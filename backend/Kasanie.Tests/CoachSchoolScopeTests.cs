using System.Net;
using System.Text.Json;
using Kasanie.Api.Domain;

namespace Kasanie.Tests;

public sealed partial class AuthorizationIntegrationTests
{
    [Fact]
    public async Task CoachSchoolScope_FiltersWorkdayAndPlayers_WithoutExpandingTeamAccess()
    {
        await using var factory = await TrainingWorkspaceFactory();
        await factory.SeedAsync(db =>
        {
            db.Teams.AddRange(new Team { Id = 72, Name = "Assigned second school", SchoolId = 71 }, new Team { Id = 73, Name = "Not assigned", SchoolId = 71 });
            db.TeamCoaches.Add(new TeamCoach { TeamId = 72, CoachId = 70 });
            db.Players.Add(new PlayerProfile { Id = 72, FirstName = "Second", LastName = "School", PreferredPosition = "Защитник", DominantFoot = "Левая", ExperienceLevel = "Любитель" });
            db.TeamPlayers.Add(new TeamPlayer { TeamId = 72, PlayerId = 72 });
            db.TeamMatches.AddRange(new TeamMatch { TeamId = 70, Opponent = "First opponent", ScheduledAt = DateTimeOffset.UtcNow.AddDays(1) }, new TeamMatch { TeamId = 72, Opponent = "Second opponent", ScheduledAt = DateTimeOffset.UtcNow.AddDays(2) });
        });
        using var client = factory.CreateClient();
        using var list = await client.SendAsync(Get("/api/coach/teams", "training-coach", Roles.Coach));
        using var teams = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(2, teams.RootElement.GetArrayLength());
        Assert.Contains(teams.RootElement.EnumerateArray(), team => team.GetProperty("schoolId").GetInt32() == 71);
        using var day = await client.SendAsync(Get("/api/coach/workday?schoolId=71", "training-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.OK, day.StatusCode);
        using var dashboard = JsonDocument.Parse(await day.Content.ReadAsStringAsync());
        Assert.Equal("Second opponent", dashboard.RootElement.GetProperty("nextMatch").GetProperty("opponent").GetString());
        using var players = await client.SendAsync(Get("/api/coach/players?schoolId=71", "training-coach", Roles.Coach));
        using var roster = JsonDocument.Parse(await players.Content.ReadAsStringAsync());
        Assert.Equal(1, roster.RootElement.GetArrayLength());
        Assert.Equal(72, roster.RootElement[0].GetProperty("id").GetInt32());
        using var unassigned = await client.SendAsync(Get("/api/coach/teams/73/command-center", "training-coach", Roles.Coach));
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        using var foreign = await client.SendAsync(Get("/api/coach/players?schoolId=999", "training-coach", Roles.Coach));
        using var empty = JsonDocument.Parse(await foreign.Content.ReadAsStringAsync());
        Assert.Equal(0, empty.RootElement.GetArrayLength());
    }
}
