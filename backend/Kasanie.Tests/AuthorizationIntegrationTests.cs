using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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
    public async Task Analyst_SeesOnlyPlayersFromClaimedRegion()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var regionA = new Municipality { Id = 1, Name = "City A", Region = "Region A" };
            var regionB = new Municipality { Id = 2, Name = "City B", Region = "Region B" };
            db.Municipalities.AddRange(regionA, regionB);
            db.Players.AddRange(
                Player(1, regionA), Player(2, regionA), Player(3, regionA),
                Player(4, regionB), Player(5, regionB), Player(6, regionB));
            db.TrainingSessions.AddRange(
                CompletedSession(1, 4), CompletedSession(2, 5), CompletedSession(3, 6));
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/analytics/overview", "analyst-a", Roles.RegionalAnalyst, "Region A"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Region A", json.RootElement.GetProperty("region").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("totalActivePlayers").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("totalCompletedWorkouts").GetInt32());
        Assert.Equal("City A", json.RootElement.GetProperty("municipalities")[0].GetProperty("municipality").GetString());
    }

    [Fact]
    public async Task Analyst_WithoutRegionClaim_IsDenied()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/analytics/overview", "analyst-a", Roles.RegionalAnalyst));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Analyst_RegionBelowPrivacyThreshold_IsFullySuppressed()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var municipality = new Municipality { Id = 1, Name = "Small City", Region = "Small Region" };
            db.Municipalities.Add(municipality);
            db.Players.AddRange(Player(1, municipality), Player(2, municipality));
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/analytics/overview", "analyst-a", Roles.RegionalAnalyst, "Small Region"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.GetProperty("suppressed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("totalActivePlayers").ValueKind);
        Assert.Empty(json.RootElement.GetProperty("municipalities").EnumerateArray());
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
    public async Task Admin_InviteAnalyst_RequiresAndStoresRegionClaim()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => db.Municipalities.Add(new Municipality { Name = "Kazan", Region = "Tatarstan" }));
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);

        using var missingRegion = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "analyst-one@example.test", role = Roles.RegionalAnalyst, region = (string?)null }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, missingRegion.StatusCode);

        using var inviteResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "analyst-one@example.test", role = Roles.RegionalAnalyst, region = "Tatarstan" }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByEmailAsync("analyst-one@example.test");
        Assert.NotNull(invited);
        var claims = await users.GetClaimsAsync(invited);
        Assert.Contains(claims, x => x.Type == KasanieClaimTypes.AnalyticsRegion && x.Value == "Tatarstan");
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
        builder.UseSetting("Analytics:MinimumGroupSize", "3");
        builder.UseSetting("App:PublicUrl", "https://prokasanie.test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            var databaseServices = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            services.AddDbContext<AppDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .UseInternalServiceProvider(databaseServices));
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
            claims.Add(new Claim(KasanieClaimTypes.AnalyticsRegion, region.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
