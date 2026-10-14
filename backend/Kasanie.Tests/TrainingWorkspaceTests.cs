using System.Net;
using System.Text.Json;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;

namespace Kasanie.Tests;

public sealed partial class AuthorizationIntegrationTests
{
    private static async Task<TestApplicationFactory> TrainingWorkspaceFactory()
    {
        var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Schools.AddRange(new School { Id = 70, Name = "Training school", Slug = "training-school" }, new School { Id = 71, Name = "Other school", Slug = "other-training-school" });
            db.Teams.AddRange(new Team { Id = 70, Name = "U17", SchoolId = 70 }, new Team { Id = 71, Name = "Other team", SchoolId = 71 });
            db.CoachProfiles.AddRange(new CoachProfile { Id = 70, DisplayName = "Main coach", UserId = "training-coach" }, new CoachProfile { Id = 71, DisplayName = "Other coach", UserId = "other-training-coach" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 70, CoachId = 70, IsHeadCoach = true });
            db.Players.Add(new PlayerProfile { Id = 70, FirstName = "Иван", LastName = "Игрок", PreferredPosition = "Защитник", DominantFoot = "Правая", ExperienceLevel = "Начальный", DateOfBirth = new DateOnly(2010, 1, 1) });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 70, PlayerId = 70 });
        });
        return factory;
    }

    private static TrainingCalendarEntry TrainingEntry(int slot = 0, string date = "2026-02-04") => new(DateOnly.Parse(date), slot, "Угловые", "SetPieces", 50, 60, "Team", null);

    [Fact]
    public async Task TrainingWorkspace_PersistsEverySection_AndRejectsStaleVersion()
    {
        await using var factory = await TrainingWorkspaceFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "training-coach", Roles.Coach);
        var state = new TrainingWorkspaceState
        {
            ResponsibleCoachId = 70,
            Tactics = [new("Основная", "4-4-2", "Пас"), new("Другой вариант", "4-3-3", null)],
            Calendar = [TrainingEntry()],
            Players = [new(70, "Defence", "Центральный защитник", "Защита", "Навесы", "Half", true, new DateOnly(2026, 2, 5), "Индивидуальная задача")],
            Mentoring = [new("one", "Группа 1", 70, [70])],
            Coaches = [new(70, ["DefenceTactics", "Strength"])],
            Satisfaction = "Обратная связь", MatchPreparation = "Стандарты", LastMatchComment = "Первый пас"
        };
        var request = new SaveTrainingWorkspaceRequest(0, 180, state);
        using var saved = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/training-workspace", request, "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var response = await client.SendAsync(Get("/api/coach/teams/70/training-workspace?year=2026&utcOffsetMinutes=180", "training-coach", Roles.Coach));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
        var stored = json.RootElement.GetProperty("state");
        Assert.Equal("Угловые", stored.GetProperty("calendar")[0].GetProperty("session").GetString());
        Assert.Equal("Навесы", stored.GetProperty("players")[0].GetProperty("focus").GetString());
        Assert.True(stored.GetProperty("players")[0].GetProperty("weakFoot").GetBoolean());
        Assert.Equal(70, stored.GetProperty("mentoring")[0].GetProperty("mentorId").GetInt32());
        Assert.Equal(2, stored.GetProperty("coaches")[0].GetProperty("tasks").GetArrayLength());
        Assert.Equal(2, stored.GetProperty("tactics").GetArrayLength());
        Assert.Equal("Стандарты", stored.GetProperty("matchPreparation").GetString());
        using var stale = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/training-workspace", request, "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Theory]
    [InlineData("match")]
    [InlineData("journal")]
    public async Task TrainingWorkspace_ProtectsMatchAndJournalSlots_InLocalTime(string source)
    {
        await using var factory = await TrainingWorkspaceFactory();
        await factory.SeedAsync(db =>
        {
            if (source == "match") db.TeamMatches.Add(new TeamMatch { Id = 70, TeamId = 70, Opponent = "Opponent", ScheduledAt = new DateTimeOffset(2026, 2, 3, 23, 30, 0, TimeSpan.Zero) });
            else db.TeamTrainings.Add(new TeamTraining { Id = 70, TeamId = 70, CoachId = 70, Title = "Journal session", ScheduledAt = new DateTimeOffset(2026, 2, 3, 23, 30, 0, TimeSpan.Zero) });
        });
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "training-coach", Roles.Coach);
        using var response = await client.SendAsync(Get("/api/coach/teams/70/training-workspace?year=2026&utcOffsetMinutes=180", "training-coach", Roles.Coach));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var locked = json.RootElement.GetProperty("lockedSlots")[0];
        Assert.Equal("2026-02-04", locked.GetProperty("date").GetString());
        var slot = locked.GetProperty("slot").GetInt32();
        var state = new TrainingWorkspaceState { Calendar = [TrainingEntry(slot)] };
        using var edit = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/training-workspace", new SaveTrainingWorkspaceRequest(0, 180, state), "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        state.Calendar = [TrainingEntry(slot == 0 ? 2 : 0)];
        using var freeSlot = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/training-workspace", new SaveTrainingWorkspaceRequest(0, 180, state), "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, freeSlot.StatusCode);
    }

    [Fact]
    public async Task TrainingWorkspace_ForbidsOtherTeamAndRejectsForeignAssignments()
    {
        await using var factory = await TrainingWorkspaceFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "training-coach", Roles.Coach);
        using var other = await client.SendAsync(Get("/api/coach/teams/71/training-workspace", "training-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        var state = new TrainingWorkspaceState { ResponsibleCoachId = 71, Players = [new(999, "Attack", "НП", "Атака", "Навесы", "Normal", false, null, null)] };
        using var foreign = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/training-workspace", new SaveTrainingWorkspaceRequest(0, 180, state), "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        using var writeOther = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/71/training-workspace", new SaveTrainingWorkspaceRequest(0, 180, new()), "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Forbidden, writeOther.StatusCode);
    }

    [Fact]
    public async Task TrainingWorkspace_RejectsDuplicateSlotsAndMentoringMemberships()
    {
        await using var factory = await TrainingWorkspaceFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "training-coach", Roles.Coach);
        var state = new TrainingWorkspaceState { Calendar = [TrainingEntry(), TrainingEntry()], Mentoring = [new("one", "Один", null, [70]), new("two", "Два", null, [70])] };
        using var invalid = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/training-workspace", new SaveTrainingWorkspaceRequest(0, 180, state), "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
