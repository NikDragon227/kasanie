using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kasanie.Tests;

public sealed partial class AuthorizationIntegrationTests
{
    private static async Task<TestApplicationFactory> KeeperFactory()
    {
        var factory = await TrainingWorkspaceFactory();
        await factory.SeedAsync(db =>
        {
            db.SchoolMemberships.AddRange(
                new SchoolMembership { SchoolId = 70, UserId = "keeper-owner", Role = SchoolMembershipRole.Owner },
                new SchoolMembership { SchoolId = 70, UserId = "keeper-coach", Role = SchoolMembershipRole.GoalkeeperCoach },
                new SchoolMembership { SchoolId = 70, UserId = "another-keeper", Role = SchoolMembershipRole.GoalkeeperCoach });
            db.CoachProfiles.AddRange(new CoachProfile { Id = 72, UserId = "keeper-coach", DisplayName = "Keeper coach" }, new CoachProfile { Id = 73, UserId = "another-keeper", DisplayName = "Another keeper coach" });
            db.GoalkeeperCoachAssignments.AddRange(new GoalkeeperCoachAssignment { TeamId = 70, CoachId = 72 }, new GoalkeeperCoachAssignment { TeamId = 70, CoachId = 73 });
            db.Players.AddRange(new PlayerProfile { Id = 72, FirstName = "Иван", LastName = "Вратарь", PreferredPosition = "Вратарь", DominantFoot = "Правая", ExperienceLevel = "Любитель" },
                new PlayerProfile { Id = 73, FirstName = "Чужой", LastName = "Вратарь", PreferredPosition = "GK", DominantFoot = "Левая", ExperienceLevel = "Любитель" });
            db.TeamPlayers.AddRange(new TeamPlayer { TeamId = 70, PlayerId = 72 }, new TeamPlayer { TeamId = 71, PlayerId = 73 });
        });
        return factory;
    }

    private static GoalkeeperTrainingRequest KeeperDraft() => new("Работа на линии", DateTimeOffset.UtcNow.AddHours(-2), "Поле", "Ловля низкого мяча", "Средняя",
        [new GoalkeeperPlanStep(null, "Ловля мяча", "Разминка", 15, "Две серии")], [new GoalkeeperParticipant(72, "Unknown", null, "")], "");

    [Fact]
    public async Task KeeperAppointment_IsSeparateFromGeneralCoach_AndCannotManageTheSchool()
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        using var teams = await client.SendAsync(Get("/api/coach/goalkeepers/teams", "keeper-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.OK, teams.StatusCode);
        Assert.Single((await teams.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        using var workspace = await client.SendAsync(Get("/api/coach/goalkeepers/teams/70/workspace", "keeper-coach", Roles.Coach));
        var players = (await workspace.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("players");
        Assert.Single(players.EnumerateArray());
        Assert.Equal(72, players[0].GetProperty("playerId").GetInt32());
        using var general = await client.SendAsync(Get("/api/coach/teams/70/command-center", "keeper-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, general.StatusCode);
        var csrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        using var schoolWrite = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/70/teams", new { name = "No", ageGroup = "U14", isActive = true }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Forbidden, schoolWrite.StatusCode);
        using var promote = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/70/teams/70/goalkeeper-coaches", new { coachId = 72 }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
    }

    [Fact]
    public async Task KeeperOwner_CanAssignAndRevoke_WithoutReplacingHeadCoachOrDeletingHistory()
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-owner", Roles.SchoolOwner);
        using var assign = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/70/teams/70/goalkeeper-coaches", new { coachId = 72 }, "keeper-owner", Roles.SchoolOwner, csrf));
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        using var foreign = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/70/teams/71/goalkeeper-coaches", new { coachId = 72 }, "keeper-owner", Roles.SchoolOwner, csrf));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var keeperCsrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        using var create = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", KeeperDraft(), "keeper-coach", Roles.Coach, keeperCsrf));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        using var revoke = await client.SendAsync(JsonRequest(HttpMethod.Delete, "/api/school/70/teams/70/goalkeeper-coaches/72", new { }, "keeper-owner", Roles.SchoolOwner, csrf));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var denied = await client.SendAsync(Get($"/api/coach/goalkeepers/trainings/{id}", "keeper-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var schoolDenied = await client.SendAsync(Get("/api/school/70/teams", "keeper-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, schoolDenied.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.TeamCoaches.AnyAsync(x => x.TeamId == 70 && x.CoachId == 70 && x.IsHeadCoach));
        Assert.True(await db.GoalkeeperTrainings.AnyAsync(x => x.Id == id));
        Assert.False(await db.TeamCoaches.AnyAsync(x => x.CoachId == 72));
    }

    [Fact]
    public async Task KeeperJournal_FullLifecycle_UpdatesAttendance_AndLocksCompletedSession()
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        var draft = KeeperDraft();
        using var create = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", draft, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        using var started = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/teams/70/trainings/{id}", draft with { Version = 1, Status = "InProgress" }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        using var downgrade = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/teams/70/trainings/{id}", draft with { Version = 2, Status = "Planned" }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Conflict, downgrade.StatusCode);
        using var stale = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/teams/70/trainings/{id}", draft with { Version = 1 }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var unknown = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/teams/70/trainings/{id}", draft with { Version = 2, Status = "Completed" }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var completed = draft with { Version = 2, Status = "Completed", Players = [new GoalkeeperParticipant(72, "Present", 8, "Уверенная ловля")], Notes = "Повторить игру ногами" };
        using var finish = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/teams/70/trainings/{id}", completed, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
        using var locked = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/teams/70/trainings/{id}", completed with { Version = 3 }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        using var report = await client.SendAsync(Get("/api/coach/goalkeepers/teams/70/workspace", "keeper-coach", Roles.Coach));
        var json = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("players")[0].GetProperty("attended").GetInt32());
        Assert.Equal(1, json.GetProperty("players")[0].GetProperty("sessions").GetInt32());
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeamTrainings.ToListAsync());
    }

    [Fact]
    public async Task KeeperData_IsPrivateToCoach_AndInactiveAppointmentHasNoAccess()
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        using var create = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", KeeperDraft(), "keeper-coach", Roles.Coach, csrf));
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        using var other = await client.SendAsync(Get($"/api/coach/goalkeepers/trainings/{id}", "another-keeper", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        using var foreign = await client.SendAsync(Get("/api/coach/goalkeepers/teams/71/workspace", "keeper-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        await factory.SeedAsync(db => { var membership = db.SchoolMemberships.Single(x => x.UserId == "keeper-coach"); membership.IsActive = false; });
        using var disabled = await client.SendAsync(Get("/api/coach/goalkeepers/teams/70/workspace", "keeper-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, disabled.StatusCode);
    }

    [Theory]
    [InlineData(70)]
    [InlineData(73)]
    public async Task KeeperPlan_RejectsOutfieldAndForeignPlayers(int playerId)
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", KeeperDraft() with { Players = [new GoalkeeperParticipant(playerId, "Unknown", null, "")] }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task KeeperLibrary_UsesSnapshots_AndRejectsOtherCoachsExercise()
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        var exercise = new GoalkeeperExerciseRequest("Ловля", "Техника", "Исходная инструкция", "Мяч", 10);
        using var created = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/exercises", exercise, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var exerciseId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var draft = KeeperDraft() with { Plan = [new GoalkeeperPlanStep(exerciseId, "Ловля", "Разминка", 10, "Исходная инструкция")] };
        using var create = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", draft, "keeper-coach", Roles.Coach, csrf));
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        using var edit = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/exercises/{exerciseId}", exercise with { Description = "Изменено", IsActive = false }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        using var detail = await client.SendAsync(Get($"/api/coach/goalkeepers/trainings/{id}", "keeper-coach", Roles.Coach));
        Assert.Equal("Исходная инструкция", (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetProperty("plan")[0].GetProperty("instructions").GetString());
        var otherCsrf = await CsrfAsync(client, "another-keeper", Roles.Coach);
        using var denied = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", draft, "another-keeper", Roles.Coach, otherCsrf));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var archived = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", draft with { ScheduledAt = draft.ScheduledAt.AddDays(1) }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, archived.StatusCode);
    }

    [Fact]
    public async Task KeeperCalendar_RejectsOverlap_AndCancelledSlotCanBeReused()
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        var draft = KeeperDraft();
        using var first = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", draft, "keeper-coach", Roles.Coach, csrf));
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        using var collision = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", draft with { ScheduledAt = draft.ScheduledAt.AddMinutes(5) }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        using var cancel = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/coach/goalkeepers/teams/70/trainings/{id}", draft with { Version = 1, Status = "Cancelled" }, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        using var replacement = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/trainings", draft, "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
    }

    [Fact]
    public async Task KeeperRecords_RejectStaleVersion_AndNewPlayersAreAlwaysGoalkeepers()
    {
        await using var factory = await KeeperFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-coach", Roles.Coach);
        using var save = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/goalkeepers/teams/70/players/72", new GoalkeeperRecordRequest("Игра ногами", "Рабочие заметки"), "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var stale = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/goalkeepers/teams/70/players/72", new GoalkeeperRecordRequest("Игра ногами", "Другое"), "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var created = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/goalkeepers/teams/70/players", new GoalkeeperCreatePlayerRequest("Новый", "Вратарь", new DateOnly(2010, 1, 1), "Обе", 33), "keeper-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        using var scope = factory.Services.CreateScope();
        var player = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Players.SingleAsync(x => x.Id == id);
        Assert.Equal("Вратарь", player.PreferredPosition);
        Assert.Null(player.UserId);
    }

    [Fact]
    public async Task KeeperAppointment_AssignsRestrictedMembership_ThatStaysRestrictedAfterRevocation()
    {
        await using var factory = await KeeperFactory();
        await factory.SeedAsync(db =>
        {
            db.GoalkeeperCoachAssignments.Remove(db.GoalkeeperCoachAssignments.Single(x => x.CoachId == 73));
            db.SchoolMemberships.Single(x => x.UserId == "another-keeper").Role = SchoolMembershipRole.Coach;
        });
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "keeper-owner", Roles.SchoolOwner);
        using var assign = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/school/70/teams/70/goalkeeper-coaches", new { coachId = 73 }, "keeper-owner", Roles.SchoolOwner, csrf));
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(SchoolMembershipRole.GoalkeeperCoach, await scope.ServiceProvider.GetRequiredService<AppDbContext>().SchoolMemberships.Where(x => x.UserId == "another-keeper").Select(x => x.Role).SingleAsync());
        using var revoke = await client.SendAsync(JsonRequest(HttpMethod.Delete, "/api/school/70/teams/70/goalkeeper-coaches/73", new { }, "keeper-owner", Roles.SchoolOwner, csrf));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var school = await client.SendAsync(Get("/api/school/70/teams", "another-keeper", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, school.StatusCode);
    }
}
