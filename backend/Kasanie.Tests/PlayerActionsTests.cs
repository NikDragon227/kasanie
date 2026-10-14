using System.Net;
using System.Text.Json;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kasanie.Tests;

public sealed partial class AuthorizationIntegrationTests
{
    [Fact]
    public async Task PlayerQuickTraining_PersistsRestAndNotes_AndRejectsForeignAccess()
    {
        await using var factory = await TrainingWorkspaceFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "training-coach", Roles.Coach);
        using var rest = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/players/70/training", new { restDays = 3, notes = "Reduce load", utcOffsetMinutes = 180 }, "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, rest.StatusCode);
        using var json = JsonDocument.Parse(await rest.Content.ReadAsStringAsync());
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).DateTime);
        Assert.Equal(today.AddDays(2).ToString("yyyy-MM-dd"), json.RootElement.GetProperty("training").GetProperty("restUntil").GetString());
        using var note = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/players/70/training", new { intensity = "Half", notes = "Second note" }, "training-coach", Roles.Coach, csrf));
        using var second = JsonDocument.Parse(await note.Content.ReadAsStringAsync());
        Assert.Equal(2, second.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Null, second.RootElement.GetProperty("training").GetProperty("restUntil").ValueKind);
        Assert.Equal("Reduce load\nSecond note", second.RootElement.GetProperty("training").GetProperty("notes").GetString());
        using var forbidden = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/71/players/70/training", new { restDays = 1 }, "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task SquadMove_PreservesHistoryAndTransfersIndividualPlan_WithoutForeignMoves()
    {
        await using var factory = await TrainingWorkspaceFactory();
        await factory.SeedAsync(db =>
        {
            db.Teams.Add(new Team { Id = 72, SchoolId = 70, Name = "Reserve" });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 72, CoachId = 70 });
            var membership = db.TeamPlayers.Single(x => x.TeamId == 70);
            membership.Goals = 5; membership.IsCaptain = true; membership.ShirtNumber = 7;
            db.Players.Add(new PlayerProfile { Id = 72, FirstName = "Other", LastName = "Player", PreferredPosition = "Защитник", ExperienceLevel = "Любитель", DominantFoot = "Правая" });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 72, PlayerId = 72, ShirtNumber = 7 });
        });
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "training-coach", Roles.Coach);
        var state = new TrainingWorkspaceState { Players = [new(70, "Defence", "Защитник", "Защита", "Навесы", "Half", false, null, "Task")], Mentoring = [new("one", "Group", 70, [70])] };
        using var plan = await client.SendAsync(JsonRequest(HttpMethod.Put, "/api/coach/teams/70/training-workspace", new SaveTrainingWorkspaceRequest(0, 180, state), "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        using var foreign = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/teams/70/players/70/move", new { destinationTeamId = 71 }, "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        using var moved = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/coach/teams/70/players/70/move", new { destinationTeamId = 72 }, "training-coach", Roles.Coach, csrf));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = await db.TeamPlayers.SingleAsync(x => x.TeamId == 70);
        Assert.False(old.IsActive); Assert.False(old.IsCaptain); Assert.Equal(5, old.Goals); Assert.NotNull(old.LeftAt);
        var target = await db.TeamPlayers.SingleAsync(x => x.TeamId == 72 && x.PlayerId == 70);
        Assert.True(target.IsActive); Assert.Null(target.ShirtNumber); Assert.Equal(0, target.Goals);
        using var targetPlan = JsonDocument.Parse((await db.TeamTrainingWorkspaces.SingleAsync(x => x.TeamId == 72)).StateJson);
        Assert.Equal("Навесы", targetPlan.RootElement.GetProperty("players")[0].GetProperty("focus").GetString());
        using var sourcePlan = JsonDocument.Parse((await db.TeamTrainingWorkspaces.SingleAsync(x => x.TeamId == 70)).StateJson);
        Assert.Equal(0, sourcePlan.RootElement.GetProperty("players").GetArrayLength());
        Assert.Equal(0, sourcePlan.RootElement.GetProperty("mentoring")[0].GetProperty("playerIds").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, sourcePlan.RootElement.GetProperty("mentoring")[0].GetProperty("mentorId").ValueKind);
    }

    [Fact]
    public async Task PlayerActionCandidates_OnlyIncludeAssignedRosters()
    {
        await using var factory = await TrainingWorkspaceFactory();
        await factory.SeedAsync(db =>
        {
            db.Players.Add(new PlayerProfile { Id = 71, FirstName = "Private", LastName = "Player", PreferredPosition = "Защитник", ExperienceLevel = "Любитель", DominantFoot = "Правая" });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 71, PlayerId = 71 });
        });
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/coach/teams/70/players/70/actions", "training-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("players").GetArrayLength());
        Assert.Equal(70, json.RootElement.GetProperty("players")[0].GetProperty("playerId").GetInt32());
        using var foreign = await client.SendAsync(Get("/api/coach/teams/71/players/71/actions", "training-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }
}
