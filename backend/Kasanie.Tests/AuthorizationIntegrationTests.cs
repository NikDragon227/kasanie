using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kasanie.Tests;

public sealed class AuthorizationIntegrationTests
{
    [Fact]
    public async Task AdminSummary_ReturnsPlatformMetricsForSelectedPeriod()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = "recent-player", UserName = "recent@example.test", Email = "recent@example.test", CreatedAt = DateTimeOffset.UtcNow.AddDays(-2), LastActiveAt = DateTimeOffset.UtcNow.AddDays(-1) },
                new ApplicationUser { Id = "old-user", UserName = "old@example.test", Email = "old@example.test", CreatedAt = DateTimeOffset.UtcNow.AddDays(-40) });
            var playerRoleId = db.Roles.Single(x => x.Name == Roles.Player).Id;
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = "recent-player", RoleId = playerRoleId });
            db.Schools.Add(new School { Id = 10, Name = "Active school", Slug = "active-school" });
            db.Teams.Add(new Team { Id = 10, SchoolId = 10, Name = "U17" });
            SeedPublicActivity(db, "organizer-a");
            db.PublicActivityParticipants.Add(new PublicActivityParticipant { PublicActivityId = 1, UserId = "recent-player", Status = PublicParticipantStatus.Confirmed, JoinedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        });
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Get("/api/admin/summary?days=7", "admin-a", Roles.Admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(7, root.GetProperty("periodDays").GetInt32());
        Assert.Equal(2, root.GetProperty("users").GetInt32());
        Assert.Equal(1, root.GetProperty("newUsers").GetInt32());
        Assert.Equal(1, root.GetProperty("activeUsers").GetInt32());
        Assert.Equal(1, root.GetProperty("activeSchools").GetInt32());
        Assert.Equal(1, root.GetProperty("activeTeams").GetInt32());
        Assert.Equal(1, root.GetProperty("publishedActivities").GetInt32());
        Assert.Equal(1, root.GetProperty("newRegistrations").GetInt32());
        Assert.Equal(7, root.GetProperty("trend").GetArrayLength());
        Assert.Equal(Roles.Player, root.GetProperty("roles")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task AdminSummary_DeniesNonAdmin()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Get("/api/admin/summary", "coach-a", Roles.Coach));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Player_CanRegisterBeforeCompletingFootballProfile()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/register", new
        {
            email = "new-player@example.test",
            password = "Kasanie-Test-2026!",
            dateOfBirth = "2008-05-12",
            firstName = "Новый",
            lastName = "Игрок"
        }, null, null, csrf));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("new-player@example.test");
        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user!, Roles.Player));
        var profile = await db.Players.SingleAsync(x => x.UserId == user!.Id);
        Assert.Null(profile.MunicipalityId);
        Assert.Empty(profile.PreferredPosition);
        Assert.Empty(profile.DominantFoot);
        Assert.Empty(profile.ExperienceLevel);
    }

    [Fact]
    public async Task DevelopmentProfile_AggregatesJournalForPlayerCoachAndParent()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Municipalities.Add(new Municipality { Id = 1, Name = "Kazan", Region = "Tatarstan" });
            db.CoachProfiles.Add(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" });
            var player = Player(1); player.UserId = "player-a"; db.Players.Add(player);
            db.ParentProfiles.Add(new ParentProfile { Id = 1, UserId = "parent-a" });
            db.ParentPlayerLinks.Add(new ParentPlayerLink { ParentId = 1, PlayerId = 1, Relationship = "Parent", ConsentAccepted = true, ConsentVersion = "test" });
            db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" });
            db.Teams.Add(new Team { Id = 1, SchoolId = 1, Name = "Team A" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 1, CoachId = 1 });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 1, PlayerId = 1 });
            db.Exercises.AddRange(
                new Exercise { Id = 100, Name = "Pass", Description = "D", Instructions = "I", SkillCategory = SkillCategory.Passing, Difficulty = 1, DurationMinutes = 10, Equipment = "Ball" },
                new Exercise { Id = 101, Name = "Run", Description = "D", Instructions = "I", SkillCategory = SkillCategory.Speed, Difficulty = 1, DurationMinutes = 10, Equipment = "Cones" });
            db.TeamTrainings.Add(new TeamTraining { Id = 1, TeamId = 1, CoachId = 1, Title = "Session", ScheduledAt = new DateTimeOffset(2026, 8, 20, 16, 0, 0, TimeSpan.Zero), Status = TeamTrainingStatus.Completed, CompletedAt = DateTimeOffset.UtcNow });
            db.TeamTrainingAttendances.Add(new TeamTrainingAttendance { TeamTrainingId = 1, PlayerId = 1, Status = AttendanceStatus.Present });
            db.TeamTrainingExercises.AddRange(
                new TeamTrainingExercise { Id = 1000, TeamTrainingId = 1, ExerciseId = 100, SortOrder = 1 },
                new TeamTrainingExercise { Id = 1001, TeamTrainingId = 1, ExerciseId = 101, SortOrder = 2 });
            db.TeamTrainingPlayerResults.AddRange(
                new TeamTrainingPlayerResult { TeamTrainingExerciseId = 1000, PlayerId = 1, IsCompleted = true, Understood = true },
                new TeamTrainingPlayerResult { TeamTrainingExerciseId = 1001, PlayerId = 1, IsCompleted = false, Understood = false });
        });
        using var client = factory.CreateClient();

        foreach (var request in new[]
        {
            Get("/api/player/development", "player-a", Roles.Player),
            Get("/api/coach/players/1/development", "coach-a", Roles.Coach),
            Get("/api/parent/children/1/development", "parent-a", Roles.Parent)
        })
        {
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(1, json.RootElement.GetProperty("completedTeamTrainings").GetInt32());
            Assert.Equal(100, json.RootElement.GetProperty("attendanceRate").GetInt32());
            Assert.Equal(50, json.RootElement.GetProperty("completionRate").GetInt32());
            Assert.Equal(50, json.RootElement.GetProperty("understandingRate").GetInt32());
            Assert.Single(json.RootElement.GetProperty("focusAreas").EnumerateArray());
            Assert.Equal("Speed", json.RootElement.GetProperty("focusAreas")[0].GetProperty("skillCategory").GetString());
        }
    }

    [Fact]
    public async Task DevelopmentProfile_DeniesUnrelatedCoachAndParent()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.AddRange(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" }, new CoachProfile { Id = 2, UserId = "coach-b", DisplayName = "Coach B" });
            db.ParentProfiles.AddRange(new ParentProfile { Id = 1, UserId = "parent-a" }, new ParentProfile { Id = 2, UserId = "parent-b" });
            db.Players.Add(Player(1));
            db.ParentPlayerLinks.Add(new ParentPlayerLink { ParentId = 2, PlayerId = 1, Relationship = "Parent", ConsentAccepted = true, ConsentVersion = "test" });
            db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" });
            db.Teams.Add(new Team { Id = 1, SchoolId = 1, Name = "Team A" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 1, CoachId = 2 });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 1, PlayerId = 1 });
        });
        using var client = factory.CreateClient();

        using var coachResponse = await client.SendAsync(Get("/api/coach/players/1/development", "coach-a", Roles.Coach));
        using var parentResponse = await client.SendAsync(Get("/api/parent/children/1/development", "parent-a", Roles.Parent));

        Assert.Equal(HttpStatusCode.Forbidden, coachResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, parentResponse.StatusCode);
    }

    [Fact]
    public async Task Coach_CompletesTeamJournal_WithAttendanceAndExerciseMarks()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.Add(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" });
            db.Players.AddRange(Player(1), Player(2));
            db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" });
            db.Teams.Add(new Team { Id = 1, SchoolId = 1, Name = "Team A" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 1, CoachId = 1 });
            db.TeamPlayers.AddRange(new TeamPlayer { TeamId = 1, PlayerId = 1 }, new TeamPlayer { TeamId = 1, PlayerId = 2 });
            db.Exercises.AddRange(
                new Exercise { Id = 100, Name = "Pass", Description = "D", Instructions = "I", SkillCategory = SkillCategory.Passing, Difficulty = 1, DurationMinutes = 10, Equipment = "Ball" },
                new Exercise { Id = 101, Name = "Run", Description = "D", Instructions = "I", SkillCategory = SkillCategory.Speed, Difficulty = 1, DurationMinutes = 10, Equipment = "Cones" });
        });
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "coach-a", Roles.Coach);
        using var createResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/team-trainings", new { teamId = 1, title = "Session", scheduledAt = DateTimeOffset.UtcNow, exerciseIds = new[] { 100, 101 } }, "coach-a", Roles.Coach, csrf));
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var trainingId = createJson.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var earlyComplete = await client.SendAsync(JsonRequest(HttpMethod.Post, $"/api/coach/team-trainings/{trainingId}/complete", new { }, "coach-a", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, earlyComplete.StatusCode);
        using var attendanceResponse = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/team-trainings/{trainingId}/attendance", new { players = new[] { new { playerId = 1, status = "Present" }, new { playerId = 2, status = "Absent" } } }, "coach-a", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.NoContent, attendanceResponse.StatusCode);
        using var detailResponse = await client.SendAsync(Get($"/api/coach/team-trainings/{trainingId}", "coach-a", Roles.Coach));
        using var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var exerciseIds = detailJson.RootElement.GetProperty("exercises").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();
        var results = exerciseIds.Select((id, index) => new { playerId = 1, teamTrainingExerciseId = id, isCompleted = true, understood = index == 0 }).ToArray();
        using var reviewResponse = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/team-trainings/{trainingId}/review", new { results, notes = "Repeat running" }, "coach-a", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.NoContent, reviewResponse.StatusCode);
        using var completeResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, $"/api/coach/team-trainings/{trainingId}/complete", new { }, "coach-a", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(TeamTrainingStatus.Completed, (await db.TeamTrainings.FindAsync(trainingId))!.Status);
        Assert.Equal(2, await db.TeamTrainingPlayerResults.CountAsync(x => x.TeamTrainingExercise.TeamTrainingId == trainingId));
        Assert.Single(await db.TeamTrainingPlayerResults.Where(x => x.TeamTrainingExercise.TeamTrainingId == trainingId && !x.Understood).ToListAsync());
    }

    [Fact]
    public async Task Coach_CannotCreateJournalForAnotherTeam()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.AddRange(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" }, new CoachProfile { Id = 2, UserId = "coach-b", DisplayName = "Coach B" });
            db.Players.Add(Player(1)); db.Schools.Add(new School { Id = 1, Name = "School B", Slug = "school-b" }); db.Teams.Add(new Team { Id = 1, SchoolId = 1, Name = "Team B" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 1, CoachId = 2 }); db.TeamPlayers.Add(new TeamPlayer { TeamId = 1, PlayerId = 1 });
            db.Exercises.Add(new Exercise { Id = 100, Name = "Pass", Description = "D", Instructions = "I", SkillCategory = SkillCategory.Passing, Difficulty = 1, DurationMinutes = 10, Equipment = "Ball" });
        });
        using var client = factory.CreateClient(); var csrf = await CsrfAsync(client, "coach-a", Roles.Coach);
        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/team-trainings", new { teamId = 1, title = "Foreign", scheduledAt = DateTimeOffset.UtcNow, exerciseIds = new[] { 100 } }, "coach-a", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_CreatesSchoolAndOwnerInvitation()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/schools", new
        {
            name = "Academy One", ownerEmail = "owner-one@example.test", city = "Казань", contactEmail = "office@example.test", phone = "+70000000000"
        }, "admin-a", Roles.Admin, csrf));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("/reset-password?", json.RootElement.GetProperty("inviteUrl").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var school = await db.Schools.SingleAsync(x => x.Name == "Academy One");
        var owner = await users.FindByEmailAsync("owner-one@example.test");
        Assert.NotNull(owner);
        Assert.True(await users.IsInRoleAsync(owner!, Roles.SchoolOwner));
        Assert.True(await db.SchoolMemberships.AnyAsync(x => x.SchoolId == school.Id && x.UserId == owner!.Id && x.Role == SchoolMembershipRole.Owner));
    }

    [Fact]
    public async Task Coach_CannotOpenUnlinkedPlayerByDirectUrl()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.Add(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" });
            db.Players.AddRange(Player(1), Player(2));
            db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" });
            db.Teams.Add(new Team { Id = 1, SchoolId = 1, Name = "Team A" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 1, CoachId = 1 });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 1, PlayerId = 1 });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/coach/players/2", "coach-a", Roles.Coach));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SchoolOwner_CannotOpenAnotherSchoolsTeams()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Schools.AddRange(
                new School { Id = 1, Name = "School A", Slug = "school-a" },
                new School { Id = 2, Name = "School B", Slug = "school-b" });
            db.SchoolMemberships.AddRange(
                new SchoolMembership { SchoolId = 1, UserId = "owner-a", Role = SchoolMembershipRole.Owner },
                new SchoolMembership { SchoolId = 2, UserId = "owner-b", Role = SchoolMembershipRole.Owner });
            db.Teams.Add(new Team { Id = 2, SchoolId = 2, Name = "Team B" });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/school/2/teams", "owner-a", Roles.SchoolOwner));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SchoolOwner_CreatesStructuredSquadAndReadsWorkspace()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" });
            db.SchoolMemberships.Add(new SchoolMembership { SchoolId = 1, UserId = "owner-a", Role = SchoolMembershipRole.Owner });
        });

        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "owner-a", Roles.SchoolOwner);
        using var create = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/1/teams", new
        {
            name = "Первый состав",
            ageGroup = "U17",
            season = "2026/27",
            isActive = true
        }, "owner-a", Roles.SchoolOwner, csrf));
        using var json = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var teamId = json.RootElement.GetProperty("id").GetInt32();

        using var workspace = await client.SendAsync(Get($"/api/school/1/teams/{teamId}/workspace", "owner-a", Roles.SchoolOwner));
        using var workspaceJson = JsonDocument.Parse(await workspace.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, workspace.StatusCode);
        Assert.Equal("U17 — Первый состав", workspaceJson.RootElement.GetProperty("team").GetProperty("displayName").GetString());
        Assert.Equal("Цикл не назначен", workspaceJson.RootElement.GetProperty("team").GetProperty("trainingCycleStage").GetString());
    }

    [Fact]
    public async Task SchoolOwner_CannotOpenAnotherSchoolsTeamWorkspace()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Schools.AddRange(new School { Id = 1, Name = "School A", Slug = "school-a" }, new School { Id = 2, Name = "School B", Slug = "school-b" });
            db.SchoolMemberships.AddRange(new SchoolMembership { SchoolId = 1, UserId = "owner-a", Role = SchoolMembershipRole.Owner }, new SchoolMembership { SchoolId = 2, UserId = "owner-b", Role = SchoolMembershipRole.Owner });
            db.Teams.Add(new Team { Id = 2, SchoolId = 2, Name = "Первый состав", AgeGroup = "U17" });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/school/2/teams/2/workspace", "owner-a", Roles.SchoolOwner));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public static IEnumerable<object[]> ForeignTeamChildEndpointCases()
    {
        yield return ["GET", "/api/school/1/teams/2/workspace", null!, HttpStatusCode.NotFound];
        yield return ["PUT", "/api/school/1/teams/2", """{"name":"Hacked","ageGroup":"U17","season":"2026/27","headCoachId":null,"isActive":true}""", HttpStatusCode.NotFound];
        yield return ["POST", "/api/school/1/teams/2/messages", """{"channel":"Owner","text":"foreign message"}""", HttpStatusCode.NotFound];
        yield return ["POST", "/api/school/1/teams/2/groups", """{"name":"Foreign group","purpose":"foreign","playerIds":[2]}""", HttpStatusCode.NotFound];
        yield return ["POST", "/api/school/1/teams/2/matches", """{"opponent":"Foreign opponent","competition":"Cup","scheduledAt":"2026-08-23T12:00:00Z","venue":"Home","status":"Planned","goalsFor":null,"goalsAgainst":null,"lineupNotes":null}""", HttpStatusCode.NotFound];
        yield return ["POST", "/api/school/1/teams/2/tournaments", """{"name":"Foreign cup","startDate":"2026-09-01","endDate":"2026-09-03","status":"Planned","placement":null,"entryFee":0,"travelCost":0,"accommodationCost":0,"mealCost":0,"equipmentCost":0,"otherCost":0,"income":0,"sourceUrl":null,"registrationDeadline":"2026-08-28"}""", HttpStatusCode.NotFound];
        yield return ["POST", "/api/school/1/teams/2/coaches", """{"coachId":2,"isHeadCoach":true}""", HttpStatusCode.NotFound];
        yield return ["DELETE", "/api/school/1/teams/2/coaches/2", null!, HttpStatusCode.NotFound];
        yield return ["POST", "/api/school/1/teams/2/players", """{"playerId":2,"shirtNumber":99}""", HttpStatusCode.NotFound];
        yield return ["DELETE", "/api/school/1/teams/2/players/2", null!, HttpStatusCode.NotFound];
        yield return ["PUT", "/api/school/1/teams/2/tactics", """{"formation":"4-3-3","notes":"must not be saved"}""", HttpStatusCode.Forbidden];
    }

    [Theory]
    [MemberData(nameof(ForeignTeamChildEndpointCases))]
    public async Task SchoolOwner_CannotAccessOrMutateForeignTeamThroughOwnSchoolRoute(string method, string path, string? body, HttpStatusCode expectedStatus)
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Schools.AddRange(
                new School { Id = 1, Name = "School A", Slug = "school-a" },
                new School { Id = 2, Name = "School B", Slug = "school-b" });
            db.SchoolMemberships.AddRange(
                new SchoolMembership { SchoolId = 1, UserId = "owner-a", Role = SchoolMembershipRole.Owner },
                new SchoolMembership { SchoolId = 2, UserId = "owner-b", Role = SchoolMembershipRole.Owner },
                new SchoolMembership { SchoolId = 2, UserId = "coach-b", Role = SchoolMembershipRole.Coach });
            db.CoachProfiles.Add(new CoachProfile { Id = 2, UserId = "coach-b", DisplayName = "Coach B" });
            db.Players.Add(Player(2));
            db.Teams.Add(new Team { Id = 2, SchoolId = 2, Name = "Foreign team", AgeGroup = "U17", TacticFormation = "4-4-2" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 2, CoachId = 2, IsHeadCoach = true });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 2, PlayerId = 2, ShirtNumber = 8 });
        });

        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "owner-a", Roles.SchoolOwner);
        using var request = PortalRequest(method, path, body, "owner-a", Roles.SchoolOwner, csrf);
        using var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var foreignTeam = await db.Teams.SingleAsync(x => x.Id == 2);
        Assert.Equal("Foreign team", foreignTeam.Name);
        Assert.Equal("4-4-2", foreignTeam.TacticFormation);
        Assert.False(await db.TeamMessages.AnyAsync());
        Assert.False(await db.TeamTrainingGroups.AnyAsync());
        Assert.False(await db.TeamMatches.AnyAsync());
        Assert.False(await db.TeamTournaments.AnyAsync());
        Assert.True(await db.TeamCoaches.AnyAsync(x => x.TeamId == 2 && x.CoachId == 2 && x.IsHeadCoach));
        Assert.True(await db.TeamPlayers.AnyAsync(x => x.TeamId == 2 && x.PlayerId == 2 && x.IsActive && x.ShirtNumber == 8));
    }

    [Fact]
    public async Task CoachList_ContainsOnlyPlayersFromAssignedTeam()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.AddRange(
                new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" },
                new CoachProfile { Id = 2, UserId = "coach-b", DisplayName = "Coach B" });
            db.Players.AddRange(Player(1), Player(2));
            db.Schools.AddRange(
                new School { Id = 1, Name = "School A", Slug = "school-a" },
                new School { Id = 2, Name = "School B", Slug = "school-b" });
            db.Teams.AddRange(new Team { Id = 1, SchoolId = 1, Name = "Team A" }, new Team { Id = 2, SchoolId = 2, Name = "Team B" });
            db.TeamCoaches.AddRange(new TeamCoach { TeamId = 1, CoachId = 1 }, new TeamCoach { TeamId = 2, CoachId = 2 });
            db.TeamPlayers.AddRange(new TeamPlayer { TeamId = 1, PlayerId = 1 }, new TeamPlayer { TeamId = 2, PlayerId = 2 });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/coach/players", "coach-a", Roles.Coach));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(1, json.RootElement[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Coach_UpdatesTacticsOnlyForAssignedTeam()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.AddRange(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" }, new CoachProfile { Id = 2, UserId = "coach-b", DisplayName = "Coach B" });
            db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" });
            db.Teams.AddRange(new Team { Id = 1, SchoolId = 1, Name = "Первый состав", AgeGroup = "U17" }, new Team { Id = 2, SchoolId = 1, Name = "Второй состав", AgeGroup = "U17" });
            db.TeamCoaches.AddRange(new TeamCoach { TeamId = 1, CoachId = 1 }, new TeamCoach { TeamId = 2, CoachId = 2 });
        });

        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "coach-a", Roles.Coach);
        using var own = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/1/tactics", new { formation = "4-3-3", notes = "Высокий прессинг" }, "coach-a", Roles.Coach, csrf));
        using var foreign = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/2/tactics", new { formation = "4-4-2", notes = "Чужой план" }, "coach-a", Roles.Coach, csrf));

        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Высокий прессинг", (await db.Teams.FindAsync(1))!.TacticNotes);
        Assert.Null((await db.Teams.FindAsync(2))!.TacticNotes);
    }

    [Fact]
    public async Task Coach_UpdatesCycleOnlyForAssignedTeam()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.AddRange(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" }, new CoachProfile { Id = 2, UserId = "coach-b", DisplayName = "Coach B" });
            db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" });
            db.Teams.AddRange(new Team { Id = 1, SchoolId = 1, Name = "A" }, new Team { Id = 2, SchoolId = 1, Name = "B" });
            db.TeamCoaches.AddRange(new TeamCoach { TeamId = 1, CoachId = 1 }, new TeamCoach { TeamId = 2, CoachId = 2 });
        });
        using var client = factory.CreateClient(); var csrf = await CsrfAsync(client, "coach-a", Roles.Coach);
        using var own = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/1/cycle", new { stage = "Соревновательный этап", startsOn = "2026-08-01", endsOn = "2026-11-30" }, "coach-a", Roles.Coach, csrf));
        using var foreign = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/2/cycle", new { stage = "Базовый этап" }, "coach-a", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode); Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal("Соревновательный этап", (await db.Teams.FindAsync(1))!.TrainingCycleStage);
    }

    [Fact]
    public async Task SchoolOwner_CannotEditCoachTactics()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => { db.Schools.Add(new School { Id = 1, Name = "School A", Slug = "school-a" }); db.SchoolMemberships.Add(new SchoolMembership { SchoolId = 1, UserId = "owner-a", Role = SchoolMembershipRole.Owner }); db.Teams.Add(new Team { Id = 1, SchoolId = 1, Name = "A" }); });
        using var client = factory.CreateClient(); var csrf = await CsrfAsync(client, "owner-a", Roles.SchoolOwner);
        using var response = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/school/1/teams/1/tactics", new { formation = "4-3-3" }, "owner-a", Roles.SchoolOwner, csrf));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Parent_CannotOpenUnlinkedChildByDirectUrl()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.ParentProfiles.Add(new ParentProfile { Id = 1, UserId = "parent-a" });
            db.Players.AddRange(Player(1), Player(2));
            db.ParentPlayerLinks.Add(new ParentPlayerLink { ParentId = 1, PlayerId = 1, Relationship = "Parent", IsPrimary = true, ConsentAccepted = true, ConsentVersion = "v1" });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/parent/children/2", "parent-a", Roles.Parent));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Player_CannotOpenAnotherPlayersTrainingSession()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var own = Player(1); own.UserId = "player-a";
            var other = Player(2); other.UserId = "player-b";
            var plan = new TrainingPlan { Id = 10, PlayerId = other.Id, WeekStart = new DateOnly(2026, 8, 17) };
            var day = new TrainingDay { Id = 20, TrainingPlan = plan, PlannedDate = new DateOnly(2026, 8, 18) };
            db.Players.AddRange(own, other);
            db.TrainingSessions.Add(new TrainingSession { Id = 30, PlayerId = other.Id, TrainingDay = day, Status = SessionStatus.InProgress });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/training/sessions/30", "player-a", Roles.Player));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegionalAnalyticsEndpoint_IsNotExposed()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/analytics/overview", "analyst-a", "RegionalAnalyst", "Region A"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_InviteLink_AllowsCoachToSetOwnPassword()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var inviteResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "new-coach@example.test", role = Roles.Coach, region = (string?)null }, "admin-a", Roles.Admin, csrf));
        var inviteBody = await inviteResponse.Content.ReadAsStringAsync();
        using var inviteJson = JsonDocument.Parse(inviteBody);

        Assert.True(inviteResponse.StatusCode == HttpStatusCode.Created, $"Expected 201, got {(int)inviteResponse.StatusCode}: {inviteBody}");
        var inviteUrl = new Uri(inviteJson.RootElement.GetProperty("inviteUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(inviteUrl.Query);
        Assert.Equal("https", inviteUrl.Scheme);
        Assert.Equal("new-coach@example.test", query["email"].ToString());

        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var resetResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Secure-Invite-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByEmailAsync("new-coach@example.test");
        Assert.NotNull(invited);
        Assert.True(invited.EmailConfirmed);
        Assert.True(await users.CheckPasswordAsync(invited, "Secure-Invite-2026!"));
        Assert.True(await users.IsInRoleAsync(invited, Roles.Coach));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.CoachProfiles.AnyAsync(x => x.UserId == invited.Id));
    }

    [Fact]
    public async Task ResetPassword_ReportsPasswordPolicyErrors_AndKeepsInviteUsable()
    {
        await using var factory = new TestApplicationFactory();
        using var adminClient = factory.CreateClient();
        var adminCsrf = await CsrfAsync(adminClient, "admin-a", Roles.Admin);
        using var inviteResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "password-policy@example.test", role = Roles.Coach, region = (string?)null }, "admin-a", Roles.Admin, adminCsrf));
        using var inviteJson = JsonDocument.Parse(await inviteResponse.Content.ReadAsStringAsync());
        var inviteUrl = new Uri(inviteJson.RootElement.GetProperty("inviteUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(inviteUrl.Query);

        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var weakPasswordResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "abcdefghij" }, null, null, inviteCsrf));
        var weakPasswordBody = await weakPasswordResponse.Content.ReadAsStringAsync();
        using var weakPasswordJson = JsonDocument.Parse(weakPasswordBody);

        Assert.True(weakPasswordResponse.StatusCode == HttpStatusCode.BadRequest, $"Expected 400, got {(int)weakPasswordResponse.StatusCode}: {weakPasswordBody}");
        Assert.True(weakPasswordJson.RootElement.GetProperty("errors").TryGetProperty("newPassword", out var passwordErrors));
        Assert.NotEmpty(passwordErrors.EnumerateArray());
        Assert.False(weakPasswordJson.RootElement.TryGetProperty("message", out _));

        using var validPasswordResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Kasanie-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, validPasswordResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByEmailAsync("password-policy@example.test");
        Assert.NotNull(invited);
        Assert.True(await users.CheckPasswordAsync(invited, "Kasanie-2026!"));
    }

    [Fact]
    public async Task Admin_BlocksInvitedUserWithoutDeletingAccount()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var inviteResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "new-parent@example.test", role = Roles.Parent, region = (string?)null }, "admin-a", Roles.Admin, csrf));
        var inviteBody = await inviteResponse.Content.ReadAsStringAsync();
        Assert.True(inviteResponse.StatusCode == HttpStatusCode.Created, $"Expected 201, got {(int)inviteResponse.StatusCode}: {inviteBody}");
        using var inviteJson = JsonDocument.Parse(inviteBody);
        var userId = inviteJson.RootElement.GetProperty("id").GetString()!;

        using var lockResponse = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/admin/users/{userId}/lock", new { locked = true }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.NoContent, lockResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByIdAsync(userId);
        Assert.NotNull(invited);
        Assert.True(await users.IsLockedOutAsync(invited));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.ParentProfiles.AnyAsync(x => x.UserId == userId));

        using var resetResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, $"/api/admin/users/{userId}/invite", new { }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.Conflict, resetResponse.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUser_ChangesPassword_WithEightCharacterMinimum()
    {
        await using var factory = new TestApplicationFactory();
        const string userId = "change-password-user";
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var users = setupScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = userId, UserName = "change-password@example.test", Email = "change-password@example.test", EmailConfirmed = true };
            var createResult = await users.CreateAsync(user, "Old-Password-2026!");
            Assert.True(createResult.Succeeded);
        }

        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, userId, Roles.Coach);
        using var wrongCurrentResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/change-password", new { currentPassword = "Wrong-Password-2026!", newPassword = "Aa1!aaaa" }, userId, Roles.Coach, csrf));
        using var wrongCurrentJson = JsonDocument.Parse(await wrongCurrentResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrentResponse.StatusCode);
        Assert.True(wrongCurrentJson.RootElement.GetProperty("errors").TryGetProperty("currentPassword", out _));

        using var changeResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/change-password", new { currentPassword = "Old-Password-2026!", newPassword = "Aa1!aaaa" }, userId, Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyUsers = verifyScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var changed = await verifyUsers.FindByIdAsync(userId);
        Assert.NotNull(changed);
        Assert.False(await verifyUsers.CheckPasswordAsync(changed, "Old-Password-2026!"));
        Assert.True(await verifyUsers.CheckPasswordAsync(changed, "Aa1!aaaa"));
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x => x.UserId == userId && x.EventType == "password_changed"));
    }

    [Fact]
    public async Task Admin_RejectsRegionalAnalystInvitation()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);

        using var inviteResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "analyst-one@example.test", role = "RegionalAnalyst" }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, inviteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByEmailAsync("analyst-one@example.test");
        Assert.Null(invited);
    }

    [Fact]
    public async Task Admin_ReissuesUnusedInvite_WithNewWorkingLink()
    {
        await using var factory = new TestApplicationFactory();
        using var adminClient = factory.CreateClient();
        var adminCsrf = await CsrfAsync(adminClient, "admin-a", Roles.Admin);
        using var createResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "reissue@example.test", role = Roles.Coach, region = (string?)null }, "admin-a", Roles.Admin, adminCsrf));
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var userId = createJson.RootElement.GetProperty("id").GetString()!;
        var originalUrl = createJson.RootElement.GetProperty("inviteUrl").GetString()!;

        using var reissueResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, $"/api/admin/users/{userId}/invite", new { }, "admin-a", Roles.Admin, adminCsrf));
        using var reissueJson = JsonDocument.Parse(await reissueResponse.Content.ReadAsStringAsync());
        var reissuedUrl = reissueJson.RootElement.GetProperty("inviteUrl").GetString()!;
        Assert.Equal(HttpStatusCode.OK, reissueResponse.StatusCode);
        Assert.NotEqual(originalUrl, reissuedUrl);

        var query = QueryHelpers.ParseQuery(new Uri(reissuedUrl).Query);
        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var resetResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Secure-Reissue-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_ResetsPassword_AfterPasswordWasSet()
    {
        await using var factory = new TestApplicationFactory();
        using var adminClient = factory.CreateClient();
        var adminCsrf = await CsrfAsync(adminClient, "admin-a", Roles.Admin);
        using var createResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "activated@example.test", role = Roles.Parent, region = (string?)null }, "admin-a", Roles.Admin, adminCsrf));
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var userId = createJson.RootElement.GetProperty("id").GetString()!;
        var inviteUrl = new Uri(createJson.RootElement.GetProperty("inviteUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(inviteUrl.Query);

        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var resetResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Secure-Activated-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using var reissueResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, $"/api/admin/users/{userId}/invite", new { }, "admin-a", Roles.Admin, adminCsrf));
        var reissueBody = await reissueResponse.Content.ReadAsStringAsync();
        using var reissueJson = JsonDocument.Parse(reissueBody);
        Assert.True(reissueResponse.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)reissueResponse.StatusCode}: {reissueBody}");

        var resetUrl = new Uri(reissueJson.RootElement.GetProperty("inviteUrl").GetString()!);
        var resetQuery = QueryHelpers.ParseQuery(resetUrl.Query);
        using var adminResetResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = resetQuery["email"].ToString(), token = resetQuery["token"].ToString(), newPassword = "Admin-Reset-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, adminResetResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var resetUser = await users.FindByIdAsync(userId);
        Assert.NotNull(resetUser);
        Assert.True(await users.CheckPasswordAsync(resetUser, "Admin-Reset-2026!"));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x => x.UserId == "admin-a" && x.EventType == "user_password_reset_by_admin" && x.EntityId == userId));
    }

    [Fact]
    public async Task PublicDiscovery_IsBrowsableWithoutAuthentication()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/public/activities?sport=football&city=Kazan&district=Centre&availableOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("Open football", json.RootElement.GetProperty("items")[0].GetProperty("activity").GetProperty("title").GetString());
        Assert.Equal("Организатор", json.RootElement.GetProperty("items")[0].GetProperty("activity").GetProperty("organizerName").GetString());
        Assert.DoesNotContain("organizer-a", body);
        Assert.DoesNotContain("contactPhone", body);
    }

    [Fact]
    public async Task PublicPlatformStats_AreBrowsableWithoutAuthenticationAndUseStoredCounts()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new ApplicationUser { Id = "user-a", UserName = "a@example.test", Email = "a@example.test" },
                new ApplicationUser { Id = "coach-a", UserName = "coach@example.test", Email = "coach@example.test" });
            db.CoachProfiles.Add(new CoachProfile { UserId = "coach-a", DisplayName = "Тренер" });
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/public/platform-stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("users").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("coaches").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("teams").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("tournaments").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("trustPercent").ValueKind);
    }

    [Fact]
    public async Task PublicDiscovery_FiltersAndSortsBySearchRadius()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));
        using var client = factory.CreateClient();

        using var nearbyResponse = await client.GetAsync("/api/public/activities?sport=football&latitude=55.7901&longitude=49.1201&radiusKm=3");
        using var farResponse = await client.GetAsync("/api/public/activities?sport=football&latitude=55.60&longitude=49.00&radiusKm=3");

        Assert.Equal(HttpStatusCode.OK, nearbyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, farResponse.StatusCode);
        using var nearby = JsonDocument.Parse(await nearbyResponse.Content.ReadAsStringAsync());
        using var far = JsonDocument.Parse(await farResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, nearby.RootElement.GetProperty("total").GetInt32());
        Assert.True(nearby.RootElement.GetProperty("items")[0].GetProperty("distanceKm").GetDouble() < 1);
        Assert.Equal(0, far.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task PublicActivity_RequiresAuthenticationToJoin()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/activities/1/join", new { }, null, null, csrf));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Adult_CanRegisterAsPublicOrganizerWithoutPlayerProfile()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => db.Municipalities.Add(new Municipality { Id = 50, Name = "Казань", Region = "Татарстан" }));
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/register-organizer", new
        {
            email = "organizer@example.test",
            password = "Organizer-2026!",
            dateOfBirth = "1990-05-12",
            displayName = "Футбол на Московской",
            city = "Казань"
        }, null, null, csrf));

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {(int)response.StatusCode}: {responseBody}");
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await users.FindByEmailAsync("organizer@example.test");
        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user!, Roles.Organizer));
        Assert.True(await db.PublicOrganizerProfiles.AnyAsync(x => x.UserId == user!.Id && x.DisplayName == "Футбол на Московской"));
        Assert.False(await db.Players.AnyAsync(x => x.UserId == user!.Id));
    }

    [Fact]
    public async Task RegisterOrganizer_SucceedsEvenWhenConfirmationEmailFails()
    {
        await using var factory = new TestApplicationFactory();
        using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITransactionalEmailSender>();
            services.AddSingleton<ITransactionalEmailSender, ThrowingEmailSender>();
        }));
        await using (var seedScope = customFactory.Services.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedDb.Municipalities.Add(new Municipality { Id = 51, Name = "Самара", Region = "Самарская область" });
            await seedDb.SaveChangesAsync();
        }
        using var client = customFactory.CreateClient();
        var csrf = await CsrfAsync(client);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/register-organizer", new
        {
            email = "organizer-email-down@example.test",
            password = "Organizer-2026!",
            dateOfBirth = "1988-03-04",
            displayName = "Организатор без письма",
            city = "Самара"
        }, null, null, csrf));

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201 despite email failure, got {(int)response.StatusCode}: {responseBody}");
        await using var scope = customFactory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("organizer-email-down@example.test");
        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user!, Roles.Organizer));
    }

    [Fact]
    public async Task Minor_CannotRegisterAsPublicOrganizer()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/register-organizer", new
        {
            email = "minor-organizer@example.test",
            password = "Organizer-2026!",
            dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-17).ToString("yyyy-MM-dd"),
            displayName = "Юный организатор",
            city = "Казань"
        }, null, null, csrf));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("minor-organizer@example.test"));
    }

    [Theory]
    [InlineData(Roles.Parent, "parent-registration@example.test")]
    [InlineData(Roles.Coach, "coach-registration@example.test")]
    public async Task Adult_CanChooseParentOrCoachDuringPublicRegistration(string role, string email)
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/register-portal-user", new
        {
            email,
            password = "Kasanie-2026!",
            dateOfBirth = "1990-05-12",
            displayName = "Алексей Клявин",
            role
        }, null, null, csrf));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {(int)response.StatusCode}: {body}");
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user!, role));
        if (role == Roles.Coach)
            Assert.True(await db.CoachProfiles.AnyAsync(x => x.UserId == user!.Id && x.DisplayName == "Алексей Клявин"));
        else
            Assert.True(await db.ParentProfiles.AnyAsync(x => x.UserId == user!.Id));
    }

    [Fact]
    public async Task PublicRegistration_DoesNotAllowPrivilegedRoleSelection()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/register-portal-user", new
        {
            email = "fake-admin@example.test",
            password = "Kasanie-2026!",
            dateOfBirth = "1990-05-12",
            displayName = "Fake Admin",
            role = Roles.Admin
        }, null, null, csrf));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("fake-admin@example.test"));
    }

    [Fact]
    public async Task AdultOrganizer_CanCreatePublicMeetingPoint()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "organizer-a", Roles.Coach);

        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/venues/", new
        {
            name = "Поле на Московской",
            city = "Казань",
            district = "Вахитовский",
            address = "ул. Московская, 1",
            latitude = 55.795,
            longitude = 49.108,
            indoor = false,
            region = "Республика Татарстан"
        }, "organizer-a", Roles.Coach, csrf));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.SportsVenues.AnyAsync(x => x.Name == "Поле на Московской" && x.City == "Казань" && !x.IsVerified));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.UserId == "organizer-a" && x.EventType == "public_venue_created"));
    }

    [Fact]
    public async Task Organizer_CanChooseWhetherToOccupyParticipantPlace()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "existing-organizer"));
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "organizer-a", Roles.Coach);
        var request = new
        {
            sportId = 1, venueId = 1, eventType = 0, gameFormat = "5×5", title = "Футбол вечером в Туле", description = "Открытая игра",
            startAt = DateTimeOffset.UtcNow.AddDays(5), endAt = DateTimeOffset.UtcNow.AddDays(5).AddHours(2), capacity = 8,
            waitlistCapacity = 2, price = 0, skillLevel = "Любой", minimumAge = 18, maximumAge = (int?)null,
            equipmentRequirements = (string?)null, rules = (string?)null, cancellationPolicy = (string?)null,
            registrationDeadline = (DateTimeOffset?)null, isRecurring = false, recurrenceRule = (string?)null,
            organizerParticipates = true
        };

        using var create = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/", request, "organizer-a", Roles.Coach, csrf));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.DoesNotContain("Футбол", create.Headers.Location?.OriginalString ?? string.Empty);
        using var createJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var activityId = createJson.RootElement.GetProperty("id").GetInt32();
        using var list = await client.SendAsync(Get("/api/organizer/activities/", "organizer-a", Roles.Coach));
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.True(listJson.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == activityId).GetProperty("organizerParticipates").GetBoolean());

        var updateRequest = new
        {
            request.sportId, request.venueId, request.eventType, request.gameFormat, request.title, request.description, request.startAt, request.endAt,
            request.capacity, request.waitlistCapacity, request.price, request.skillLevel, request.minimumAge, request.maximumAge,
            request.equipmentRequirements, request.rules, request.cancellationPolicy, request.registrationDeadline, request.isRecurring,
            request.recurrenceRule, organizerParticipates = false
        };
        using var update = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/organizer/activities/{activityId}", updateRequest, "organizer-a", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PublicParticipantStatus.Cancelled, (await db.PublicActivityParticipants.SingleAsync(x => x.PublicActivityId == activityId && x.UserId == "organizer-a")).Status);
        Assert.Equal("5×5", (await db.PublicActivities.SingleAsync(x => x.Id == activityId)).GameFormat);
    }

    [Fact]
    public async Task Organizer_HockeyFormatCountsSkatersAndGoalkeeperSeparately()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "existing-organizer"));
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "organizer-a", Roles.Coach);
        var startAt = DateTimeOffset.UtcNow.AddDays(6);
        object Request(string gameFormat, string title) => new
        {
            sportId = 2, venueId = 1, eventType = 0, gameFormat, title, description = "Открытая хоккейная игра",
            startAt, endAt = startAt.AddHours(2), capacity = 12, waitlistCapacity = 0, price = 0,
            skillLevel = "Любой", minimumAge = 18, maximumAge = (int?)null,
            equipmentRequirements = (string?)null, rules = (string?)null, cancellationPolicy = (string?)null,
            registrationDeadline = (DateTimeOffset?)null, isRecurring = false, recurrenceRule = (string?)null,
            organizerParticipates = true
        };

        using var ambiguous = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/", Request("5×5", "Старое обозначение"), "organizer-a", Roles.Coach, csrf));
        using var standard = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/", Request("5+1", "Классический хоккей"), "organizer-a", Roles.Coach, csrf));

        Assert.Equal(HttpStatusCode.BadRequest, ambiguous.StatusCode);
        Assert.Equal(HttpStatusCode.Created, standard.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("5+1", (await db.PublicActivities.SingleAsync(x => x.Title == "Классический хоккей")).GameFormat);
    }

    [Fact]
    public async Task Organizer_CanArchiveOwnCancelledActivityButCannotDeletePublishedOrForeignActivity()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));

        using var foreignClient = factory.CreateClient();
        var foreignCsrf = await CsrfAsync(foreignClient, "organizer-b", Roles.Organizer);
        using var foreignDelete = await foreignClient.SendAsync(JsonRequest(HttpMethod.Delete, "/api/organizer/activities/1", new { }, "organizer-b", Roles.Organizer, foreignCsrf));
        Assert.Equal(HttpStatusCode.Forbidden, foreignDelete.StatusCode);

        using var ownerClient = factory.CreateClient();
        var ownerCsrf = await CsrfAsync(ownerClient, "organizer-a", Roles.Organizer);
        using var publishedDelete = await ownerClient.SendAsync(JsonRequest(HttpMethod.Delete, "/api/organizer/activities/1", new { }, "organizer-a", Roles.Organizer, ownerCsrf));
        Assert.Equal(HttpStatusCode.Conflict, publishedDelete.StatusCode);

        using var cancel = await ownerClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/cancel", new { }, "organizer-a", Roles.Organizer, ownerCsrf));
        using var delete = await ownerClient.SendAsync(JsonRequest(HttpMethod.Delete, "/api/organizer/activities/1", new { }, "organizer-a", Roles.Organizer, ownerCsrf));
        using var list = await ownerClient.SendAsync(Get("/api/organizer/activities/", "organizer-a", Roles.Organizer));

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Empty(listJson.RootElement.EnumerateArray());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PublicActivityStatus.Archived, (await db.PublicActivities.SingleAsync()).Status);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.UserId == "organizer-a" && x.EventType == "public_activity_deleted" && x.EntityId == "1"));
    }

    [Fact]
    public async Task PublicActivity_PreventsOrganizerIdorAndAllowsAdultJoin()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));
        using var client = factory.CreateClient();
        var foreignCsrf = await CsrfAsync(client, "organizer-b", Roles.Coach);

        using var foreignCancel = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/cancel", new { }, "organizer-b", Roles.Coach, foreignCsrf));
        Assert.Equal(HttpStatusCode.Forbidden, foreignCancel.StatusCode);

        using var adultClient = factory.CreateClient();
        var joinCsrf = await CsrfAsync(adultClient, "adult-a", Roles.Coach);
        using var join = await adultClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/activities/1/join", new { }, "adult-a", Roles.Coach, joinCsrf));
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PublicActivityStatus.Published, (await db.PublicActivities.SingleAsync()).Status);
        Assert.True(await db.PublicActivityParticipants.AnyAsync(x => x.PublicActivityId == 1 && x.UserId == "adult-a" && x.Status == PublicParticipantStatus.Confirmed));
    }

    [Fact]
    public async Task PublicActivity_PersistsParticipationAndRejectsDuplicateJoin()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "adult-a", Roles.Coach);

        using var join = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/activities/1/join", new { }, "adult-a", Roles.Coach, csrf));
        using var duplicate = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/activities/1/join", new { }, "adult-a", Roles.Coach, csrf));
        using var participation = await client.SendAsync(Get("/api/activities/1/participation", "adult-a", Roles.Coach));
        using var mine = await client.SendAsync(Get("/api/activities/mine", "adult-a", Roles.Coach));

        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, participation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        using var participationJson = JsonDocument.Parse(await participation.Content.ReadAsStringAsync());
        using var mineJson = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
        Assert.Equal("Confirmed", participationJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("Open football", mineJson.RootElement[0].GetProperty("activity").GetProperty("title").GetString());
        Assert.Equal("Confirmed", mineJson.RootElement[0].GetProperty("participation").GetProperty("status").GetString());
    }

    [Fact]
    public async Task PublicActivity_AllowsAdultGuestToJoinWithoutAccountAndSharesContactOnlyWithOrganizer()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));
        using var guestClient = factory.CreateClient();
        var guestCsrf = await CsrfAsync(guestClient);

        var payload = new { name = "Алексей", contact = "@alexey_sport", adultConfirmed = true };
        using var join = await guestClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/public/activities/1/guest-join", payload, null, null, guestCsrf));
        using var duplicate = await guestClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/public/activities/1/guest-join", payload, null, null, guestCsrf));

        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var joinJson = JsonDocument.Parse(await join.Content.ReadAsStringAsync());
        var cancellationToken = joinJson.RootElement.GetProperty("cancellationToken").GetString()!;
        Assert.Equal(64, cancellationToken.Length);
        Assert.Equal($"/guest/participations/{cancellationToken}", joinJson.RootElement.GetProperty("managePath").GetString());
        using var guestManage = await guestClient.GetAsync($"/api/public/guest-participations/{cancellationToken}");
        var guestManageBody = await guestManage.Content.ReadAsStringAsync();
        Assert.True(guestManage.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)guestManage.StatusCode}: {guestManageBody}");
        Assert.Contains("Алексей", guestManageBody);
        Assert.DoesNotContain("@alexey_sport", guestManageBody);
        using var organizerClient = factory.CreateClient();
        using var organizerList = await organizerClient.SendAsync(Get("/api/organizer/activities/1/participants", "organizer-a", Roles.Organizer));
        var organizerBody = await organizerList.Content.ReadAsStringAsync();
        Assert.Contains("Алексей", organizerBody);
        Assert.Contains("@alexey_sport", organizerBody);

        using var publicDetail = await guestClient.GetAsync("/api/public/activities/open-football");
        Assert.DoesNotContain("@alexey_sport", await publicDetail.Content.ReadAsStringAsync());
        using var cancel = await guestClient.SendAsync(JsonRequest(HttpMethod.Post, $"/api/public/guest-participations/{cancellationToken}/cancel", new { }, null, null, guestCsrf));
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var participant = await db.PublicActivityParticipants.SingleAsync();
        Assert.Null(participant.UserId);
        Assert.Equal("guest-web", participant.Source);
        Assert.Equal(PublicParticipantStatus.Cancelled, participant.Status);
        Assert.NotEqual(cancellationToken, participant.GuestCancellationTokenHash);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EventType == "public_activity_guest_joined" && x.UserId == null));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EventType == "public_activity_guest_left" && x.UserId == null));
    }

    [Fact]
    public async Task PublicActivity_LeavingConfirmedPlacePromotesOldestWaitlistedParticipant()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            SeedPublicActivity(db, "organizer-a");
            var activity = db.PublicActivities.Local.Single();
            activity.Capacity = 1;
            activity.Status = PublicActivityStatus.Full;
            db.PublicActivityParticipants.AddRange(
                new PublicActivityParticipant { Id = 10, PublicActivityId = 1, UserId = "confirmed-a", Status = PublicParticipantStatus.Confirmed, JoinedAt = DateTimeOffset.UtcNow.AddHours(-3), ConfirmedAt = DateTimeOffset.UtcNow.AddHours(-3) },
                new PublicActivityParticipant { Id = 11, PublicActivityId = 1, UserId = "wait-oldest", Status = PublicParticipantStatus.Waitlisted, JoinedAt = DateTimeOffset.UtcNow.AddHours(-2) },
                new PublicActivityParticipant { Id = 12, PublicActivityId = 1, UserId = "wait-latest", Status = PublicParticipantStatus.Waitlisted, JoinedAt = DateTimeOffset.UtcNow.AddHours(-1) });
        });
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "confirmed-a", Roles.Coach);

        using var leave = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/activities/1/leave", new { }, "confirmed-a", Roles.Coach, csrf));

        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PublicParticipantStatus.Cancelled, (await db.PublicActivityParticipants.SingleAsync(x => x.UserId == "confirmed-a")).Status);
        Assert.Equal(PublicParticipantStatus.Confirmed, (await db.PublicActivityParticipants.SingleAsync(x => x.UserId == "wait-oldest")).Status);
        Assert.Equal(PublicParticipantStatus.Waitlisted, (await db.PublicActivityParticipants.SingleAsync(x => x.UserId == "wait-latest")).Status);
        Assert.Equal(PublicActivityStatus.Full, (await db.PublicActivities.SingleAsync()).Status);
    }

    [Fact]
    public async Task PublicActivity_OrganizerParticipantsAreOwnerScopedAndNotPublic()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            SeedPublicActivity(db, "organizer-a");
            db.Players.Add(new PlayerProfile
            {
                Id = 501, UserId = "participant-a", FirstName = "Скрытое", LastName = "Имя",
                DateOfBirth = new DateOnly(1990, 1, 1), PreferredPosition = "", DominantFoot = "", ExperienceLevel = ""
            });
            db.PublicActivityParticipants.Add(new PublicActivityParticipant
            {
                Id = 20, PublicActivityId = 1, UserId = "participant-a", Status = PublicParticipantStatus.Confirmed,
                JoinedAt = DateTimeOffset.UtcNow.AddHours(-1), ConfirmedAt = DateTimeOffset.UtcNow.AddHours(-1)
            });
            db.PublicActivityParticipants.Add(new PublicActivityParticipant
            {
                Id = 21, PublicActivityId = 1, UserId = "waitlisted-a", Status = PublicParticipantStatus.Waitlisted,
                JoinedAt = DateTimeOffset.UtcNow
            });
            db.PublicActivities.Local.Single().Capacity = 1;
            db.PublicActivities.Local.Single().Status = PublicActivityStatus.Full;
        });
        using var client = factory.CreateClient();
        var foreignCsrf = await CsrfAsync(client, "organizer-b", Roles.Coach);

        using var publicDetail = await client.GetAsync("/api/public/activities/open-football");
        using var foreignList = await client.SendAsync(Get("/api/organizer/activities/1/participants", "organizer-b", Roles.Coach));
        using var foreignDelete = await client.SendAsync(JsonRequest(HttpMethod.Delete, "/api/organizer/activities/1/participants/20", new { }, "organizer-b", Roles.Coach, foreignCsrf));
        using var ownerClient = factory.CreateClient();
        var ownerCsrf = await CsrfAsync(ownerClient, "organizer-a", Roles.Coach);
        using var ownerList = await ownerClient.SendAsync(Get("/api/organizer/activities/1/participants", "organizer-a", Roles.Coach));
        using var ownerDelete = await ownerClient.SendAsync(JsonRequest(HttpMethod.Delete, "/api/organizer/activities/1/participants/20", new { }, "organizer-a", Roles.Coach, ownerCsrf));

        Assert.Equal(HttpStatusCode.OK, publicDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignDelete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerList.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ownerDelete.StatusCode);
        Assert.DoesNotContain("Скрытое", await publicDetail.Content.ReadAsStringAsync());
        Assert.Contains("Скрытое Имя", await ownerList.Content.ReadAsStringAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PublicParticipantStatus.Rejected, (await db.PublicActivityParticipants.SingleAsync(x => x.Id == 20)).Status);
        Assert.Equal(PublicParticipantStatus.Confirmed, (await db.PublicActivityParticipants.SingleAsync(x => x.Id == 21)).Status);
    }

    [Fact]
    public async Task MinorPlayer_CannotJoinPublicActivity()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            SeedPublicActivity(db, "organizer-a");
            db.Players.Add(new PlayerProfile
            {
                Id = 502, UserId = "minor-a", FirstName = "Юный", LastName = "Игрок",
                DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-16), PreferredPosition = "", DominantFoot = "", ExperienceLevel = ""
            });
        });
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "minor-a", Roles.Player);

        using var join = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/activities/1/join", new { }, "minor-a", Roles.Player, csrf));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, join.StatusCode);
    }

    private static void SeedPublicActivity(AppDbContext db, string organizerId)
    {
        db.Sports.AddRange(
            new Sport { Id = 1, Slug = "football", Name = "Football" },
            new Sport { Id = 2, Slug = "hockey", Name = "Hockey" });
        db.SportsVenues.Add(new SportsVenue
        {
            Id = 1,
            Slug = "stadium",
            Name = "Stadium",
            Region = "Tatarstan",
            City = "Kazan",
            District = "Centre",
            Address = "One Street",
            Latitude = 55.79,
            Longitude = 49.12
        });
        db.PublicActivities.Add(new PublicActivity
        {
            Id = 1,
            Slug = "open-football",
            SportId = 1,
            SportsVenueId = 1,
            OrganizerId = organizerId,
            EventType = PublicActivityType.Game,
            GameFormat = "5×5",
            Title = "Open football",
            Description = "Adults play football",
            StartAt = DateTimeOffset.UtcNow.AddDays(2),
            EndAt = DateTimeOffset.UtcNow.AddDays(2).AddHours(2),
            Capacity = 10,
            WaitlistCapacity = 2,
            Status = PublicActivityStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow
        });
    }

    private static async Task<string> CsrfAsync(HttpClient client, string? userId = null, string? role = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        if (userId is not null) request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        if (role is not null) request.Headers.Add(TestAuthHandler.RoleHeader, role);
        using var response = await client.SendAsync(request);
        var cookie = response.Headers.GetValues("Set-Cookie").Select(x => x.Split(';', 2)[0]).Single(x => x.StartsWith("Kasanie.Antiforgery=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, object body, string? userId, string? role, string csrf)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (userId is not null) request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        if (role is not null) request.Headers.Add(TestAuthHandler.RoleHeader, role);
        return request;
    }

    private static HttpRequestMessage PortalRequest(string method, string path, string? body, string userId, string role, string csrf)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.RoleHeader, role);
        return request;
    }

    private static HttpRequestMessage Get(string path, string userId, string role, string? region = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.RoleHeader, role);
        if (region is not null) request.Headers.Add(TestAuthHandler.RegionHeader, region);
        return request;
    }

    private static PlayerProfile Player(int id, Municipality? municipality = null) => new()
    {
        Id = id,
        FirstName = "Player",
        LastName = id.ToString(),
        DateOfBirth = new DateOnly(2010, 1, 1),
        MunicipalityId = municipality?.Id ?? 1,
        Municipality = municipality!,
        PreferredPosition = "Midfielder",
        DominantFoot = "Right",
        ExperienceLevel = "Amateur"
    };

    private static TrainingSession CompletedSession(int id, int playerId)
    {
        var plan = new TrainingPlan { Id = 100 + id, PlayerId = playerId, WeekStart = new DateOnly(2026, 8, 17) };
        var day = new TrainingDay { Id = 200 + id, TrainingPlan = plan, PlannedDate = new DateOnly(2026, 8, 18) };
        return new TrainingSession { Id = 300 + id, PlayerId = playerId, TrainingDay = day, Status = SessionStatus.Completed, CompletedAt = DateTimeOffset.UtcNow };
    }
}

internal sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"kasanie-auth-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("Analytics:MinimumGroupSize", "3");
        builder.UseSetting("App:PublicUrl", "https://prokasanie.test");
        builder.UseSetting("PublicDiscovery:Enabled", "true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<ITransactionalEmailSender>();
            var databaseServices = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            services.AddDbContext<AppDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .UseInternalServiceProvider(databaseServices));
            services.AddSingleton<ITransactionalEmailSender, TestEmailSender>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }
}

internal sealed class TestEmailSender : ITransactionalEmailSender
{
    public Task SendAsync(string recipient, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class ThrowingEmailSender : ITransactionalEmailSender
{
    public Task SendAsync(string recipient, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("SMTP provider rejected the recipient (5.1.10).");
}

internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-User";
    public const string RoleHeader = "X-Test-Role";
    public const string RegionHeader = "X-Test-Region";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || !Request.Headers.TryGetValue(RoleHeader, out var role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };
        if (Request.Headers.TryGetValue(RegionHeader, out var region))
            claims.Add(new Claim("kasanie:analytics-region", region.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
