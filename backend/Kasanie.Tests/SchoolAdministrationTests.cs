using System.Net;
using System.Text.Json;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kasanie.Tests;

public sealed partial class AuthorizationIntegrationTests
{
    private static async Task<TestApplicationFactory> SchoolAdministrationFactory(bool anotherOwnership = false)
    {
        var factory = new TestApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var old = new ApplicationUser { Id = "old-owner", Email = "old@school.test", UserName = "old@school.test" };
            Assert.True((await users.CreateAsync(old, "Valid-Password-2026!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(old, Roles.SchoolOwner)).Succeeded);
            var coach = new ApplicationUser { Id = "school-coach", Email = "coach@school.test", UserName = "coach@school.test" };
            Assert.True((await users.CreateAsync(coach, "Valid-Password-2026!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(coach, Roles.Coach)).Succeeded);
        }
        await factory.SeedAsync(db =>
        {
            db.Schools.AddRange(new School { Id = 90, Name = "School", Slug = "school-admin-test" }, new School { Id = 91, Name = "Blocked", Slug = "school-blocked-test", IsActive = false });
            db.SchoolMemberships.AddRange(new SchoolMembership { SchoolId = 90, UserId = "old-owner", Role = SchoolMembershipRole.Owner }, new SchoolMembership { SchoolId = 90, UserId = "school-coach", Role = SchoolMembershipRole.Coach });
            if (anotherOwnership) db.SchoolMemberships.Add(new SchoolMembership { SchoolId = 91, UserId = "old-owner", Role = SchoolMembershipRole.Owner });
            db.CoachProfiles.Add(new CoachProfile { Id = 90, UserId = "school-coach", DisplayName = "Coach" });
            db.Teams.AddRange(new Team { Id = 90, SchoolId = 90, Name = "First" }, new Team { Id = 91, SchoolId = 91, Name = "Other" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 90, CoachId = 90 });
        });
        return factory;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admin_ChangesSchoolOwner_AndRevokesOnlyUnneededGlobalRole(bool anotherOwnership)
    {
        await using var factory = await SchoolAdministrationFactory(anotherOwnership);
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var changed = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/admin/schools/90/owner", new { ownerEmail = "new@school.test", expectedOwnerEmail = "old@school.test" }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var json = JsonDocument.Parse(await changed.Content.ReadAsStringAsync());
        Assert.Equal("new@school.test", json.RootElement.GetProperty("ownerEmail").GetString());
        Assert.Contains("/reset-password?", json.RootElement.GetProperty("inviteUrl").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var old = await users.FindByIdAsync("old-owner");
        Assert.Equal(anotherOwnership, await users.IsInRoleAsync(old!, Roles.SchoolOwner));
        var previous = await db.SchoolMemberships.SingleAsync(x => x.SchoolId == 90 && x.UserId == "old-owner");
        Assert.False(previous.IsActive);
        Assert.NotEqual(SchoolMembershipRole.Owner, previous.Role);
        Assert.Single(await db.SchoolMemberships.Where(x => x.SchoolId == 90 && x.IsActive && x.Role == SchoolMembershipRole.Owner).ToListAsync());
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EventType == "school_owner_changed"));
        using var oldAccess = await client.SendAsync(Get("/api/school/90/overview", "old-owner", Roles.SchoolOwner));
        Assert.Equal(HttpStatusCode.Forbidden, oldAccess.StatusCode);
    }

    [Fact]
    public async Task AdminOwnerChange_RejectsNonAdminAndStaleOwner()
    {
        await using var factory = await SchoolAdministrationFactory();
        using var client = factory.CreateClient();
        var ownerCsrf = await CsrfAsync(client, "old-owner", Roles.SchoolOwner);
        using var forbidden = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/admin/schools/90/owner", new { ownerEmail = "new@school.test" }, "old-owner", Roles.SchoolOwner, ownerCsrf));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var stale = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/admin/schools/90/owner", new { ownerEmail = "new@school.test", expectedOwnerEmail = "stale@school.test" }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Admin_ManagesAllSchools_AndCannotEditTeamThroughWrongSchool()
    {
        await using var factory = await SchoolAdministrationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var memberships = await client.SendAsync(Get("/api/school/memberships", "admin-a", Roles.Admin));
        using var json = JsonDocument.Parse(await memberships.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetArrayLength());
        using var blocked = await client.SendAsync(Get("/api/school/91/overview", "admin-a", Roles.Admin));
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        using var tactics = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/school/90/teams/90/tactics", new TeamTacticRequest("4-4-2", "Plan"), "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.NoContent, tactics.StatusCode);
        using var foreign = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/admin/schools/90/teams/91/cycle", new TeamCycleRequest("Базовый этап", null, null), "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Admin_DisablesSchoolCoach_AndRemovesTeamAccess()
    {
        await using var factory = await SchoolAdministrationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var disabled = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/admin/schools/90/coaches/school-coach/status", new SchoolStatusRequest(false), "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        using var coachAccess = await client.SendAsync(Get("/api/coach/teams/90/training-workspace", "school-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, coachAccess.StatusCode);
        using var ownerDisabled = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/admin/schools/90/coaches/old-owner/status", new SchoolStatusRequest(false), "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.Conflict, ownerDisabled.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.TeamCoaches.Where(x => x.TeamId == 90).ToListAsync());
    }

    [Fact]
    public async Task Admin_InvitesExistingCoachFromAnotherSchool_AndAssignsTeam()
    {
        await using var factory = await SchoolAdministrationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var invited = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/91/coaches", new { email = "coach@school.test", displayName = "Coach" }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        using var assigned = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/91/teams/91/coaches", new { coachId = 90, isHeadCoach = true }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
    }
}
