using Kasanie.Api.Application;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapAnalytics(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/analytics/overview", async (IConfiguration configuration, AppDbContext db) =>
        {
            var minimum = Math.Max(2, configuration.GetValue("Analytics:MinimumGroupSize", configuration.GetValue("ANALYTICS_MINIMUM_GROUP_SIZE", 3)));
            var now = DateTimeOffset.UtcNow;
            var players = await db.Players.AsNoTracking().Select(x => new { x.Id, x.DateOfBirth, x.MunicipalityId, Municipality = x.Municipality.Name, x.CreatedAt }).ToListAsync();
            var sessions = await db.TrainingSessions.AsNoTracking().Where(x => x.Status == SessionStatus.Completed).Select(x => new { x.PlayerId, x.CompletedAt }).ToListAsync();
            var assessments = await db.AssessmentSessions.AsNoTracking().Where(x => x.IsCompleted).Select(x => new { x.PlayerId, x.CompletedAt }).ToListAsync();
            var latestSkills = await db.SkillSnapshots.AsNoTracking().GroupBy(x => x.PlayerId).Select(x => x.OrderByDescending(s => s.CapturedAt).First()).ToListAsync();
            var active7 = sessions.Where(x => x.CompletedAt >= now.AddDays(-7)).Select(x => x.PlayerId).Distinct().Count();
            var active30 = sessions.Where(x => x.CompletedAt >= now.AddDays(-30)).Select(x => x.PlayerId).Distinct().Count();
            var totalPlanned = await db.TrainingSessions.CountAsync();
            var ageGroups = players.GroupBy(x => AgeBand(AgePolicy.GetAge(x.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow))))
                .Where(x => x.Count() >= minimum).Select(x => new { group = x.Key, count = x.Count() }).OrderBy(x => x.group).ToList();
            var municipalities = players.GroupBy(x => new { x.MunicipalityId, x.Municipality }).Where(x => x.Count() >= minimum)
                .Select(x => new { municipality = x.Key.Municipality, count = x.Count() }).OrderByDescending(x => x.count).ToList();
            var trend = sessions.Where(x => x.CompletedAt >= now.AddDays(-28)).GroupBy(x => DateOnly.FromDateTime(x.CompletedAt!.Value.UtcDateTime))
                .Where(x => x.Count() >= minimum).Select(x => new { date = x.Key, completed = x.Count() }).OrderBy(x => x.date).ToList();
            object[] skillDistribution = latestSkills.Count < minimum ? [] : new object[]
            {
                new { skill = "Скорость", average = (int)Math.Round(latestSkills.Average(x => x.Speed)) },
                new { skill = "Выносливость", average = (int)Math.Round(latestSkills.Average(x => x.Endurance)) },
                new { skill = "Контроль мяча", average = (int)Math.Round(latestSkills.Average(x => x.BallControl)) },
                new { skill = "Передачи", average = (int)Math.Round(latestSkills.Average(x => x.Passing)) },
                new { skill = "Удары", average = (int)Math.Round(latestSkills.Average(x => x.Shooting)) },
                new { skill = "Ловкость", average = (int)Math.Round(latestSkills.Average(x => x.Agility)) }
            };
            return Results.Ok(new
            {
                minimumGroupSize = minimum,
                totalActivePlayers = players.Count,
                activePlayersLast7Days = active7,
                activePlayersLast30Days = active30,
                averageWorkoutCompletion = totalPlanned == 0 ? 0 : (int)Math.Round(sessions.Count * 100m / totalPlanned),
                totalCompletedWorkouts = sessions.Count,
                assessmentParticipation = players.Count == 0 ? 0 : (int)Math.Round(assessments.Select(x => x.PlayerId).Distinct().Count() * 100m / players.Count),
                ageGroups,
                municipalities,
                skillDistribution,
                engagementTrend = trend,
                privacyNotice = $"Строки групп менее {minimum} игроков подавлены. Ответ не содержит имён, email, дат рождения и идентификаторов игроков."
            });
        }).RequireAuthorization(Roles.RegionalAnalyst).WithTags("Regional analytics");
    }

    private static string AgeBand(int age) => age switch { < 10 => "до 10", < 12 => "10–11", < 14 => "12–13", < 16 => "14–15", < 18 => "16–17", _ => "18+" };
}
