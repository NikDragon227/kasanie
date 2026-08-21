using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapAnalytics(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/analytics/overview", async (ClaimsPrincipal user, IConfiguration configuration, AppDbContext db) =>
        {
            var region = user.FindFirstValue(KasanieClaimTypes.AnalyticsRegion);
            if (string.IsNullOrWhiteSpace(region)) return Results.Forbid();

            var minimum = Math.Max(2, configuration.GetValue("Analytics:MinimumGroupSize", configuration.GetValue("ANALYTICS_MINIMUM_GROUP_SIZE", 3)));
            var now = DateTimeOffset.UtcNow;
            var players = await db.Players.AsNoTracking().Where(x => x.Municipality.Region == region)
                .Select(x => new { x.Id, x.DateOfBirth, x.MunicipalityId, Municipality = x.Municipality.Name }).ToListAsync();

            if (players.Count < minimum)
            {
                return Results.Ok(new
                {
                    region,
                    minimumGroupSize = minimum,
                    suppressed = true,
                    totalActivePlayers = (int?)null,
                    activePlayersLast7Days = (int?)null,
                    activePlayersLast30Days = (int?)null,
                    averageWorkoutCompletion = (int?)null,
                    totalCompletedWorkouts = (int?)null,
                    assessmentParticipation = (int?)null,
                    ageGroups = Array.Empty<object>(),
                    municipalities = Array.Empty<object>(),
                    skillDistribution = Array.Empty<object>(),
                    engagementTrend = Array.Empty<object>(),
                    privacyNotice = $"Данные региона скрыты: в выборке меньше {minimum} игроков."
                });
            }

            var playerIds = players.Select(x => x.Id).ToArray();
            var allSessions = await db.TrainingSessions.AsNoTracking().Where(x => playerIds.Contains(x.PlayerId))
                .Select(x => new { x.PlayerId, x.Status, x.CompletedAt }).ToListAsync();
            var sessions = allSessions.Where(x => x.Status == SessionStatus.Completed).ToList();
            var assessments = await db.AssessmentSessions.AsNoTracking().Where(x => playerIds.Contains(x.PlayerId) && x.IsCompleted)
                .Select(x => new { x.PlayerId }).ToListAsync();
            var latestSkills = await db.SkillSnapshots.AsNoTracking().Where(x => playerIds.Contains(x.PlayerId)).GroupBy(x => x.PlayerId)
                .Select(x => x.OrderByDescending(s => s.CapturedAt).First()).ToListAsync();
            var active7 = sessions.Where(x => x.CompletedAt >= now.AddDays(-7)).Select(x => x.PlayerId).Distinct().Count();
            var active30 = sessions.Where(x => x.CompletedAt >= now.AddDays(-30)).Select(x => x.PlayerId).Distinct().Count();
            var sessionContributors = allSessions.Select(x => x.PlayerId).Distinct().Count();
            var completedContributors = sessions.Select(x => x.PlayerId).Distinct().Count();
            var assessmentContributors = assessments.Select(x => x.PlayerId).Distinct().Count();

            var ageBuckets = players.GroupBy(x => AgeBand(AgePolicy.GetAge(x.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)))).ToList();
            var ageGroups = ageBuckets.Any(x => x.Count() < minimum)
                ? []
                : ageBuckets.Select(x => new { group = x.Key, count = x.Count() }).OrderBy(x => x.group).ToList();
            var municipalityBuckets = players.GroupBy(x => new { x.MunicipalityId, x.Municipality }).ToList();
            var municipalities = municipalityBuckets.Any(x => x.Count() < minimum)
                ? []
                : municipalityBuckets.Select(x => new { municipality = x.Key.Municipality, count = x.Count() }).OrderByDescending(x => x.count).ToList();
            var trend = sessions.Where(x => x.CompletedAt >= now.AddDays(-28)).GroupBy(x => DateOnly.FromDateTime(x.CompletedAt!.Value.UtcDateTime))
                .Where(x => x.Select(s => s.PlayerId).Distinct().Count() >= minimum)
                .Select(x => new { date = x.Key, completed = x.Count() }).OrderBy(x => x.date).ToList();
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
                region,
                minimumGroupSize = minimum,
                suppressed = false,
                totalActivePlayers = players.Count,
                activePlayersLast7Days = SuppressSmallCount(active7, active7, minimum),
                activePlayersLast30Days = SuppressSmallCount(active30, active30, minimum),
                averageWorkoutCompletion = allSessions.Count == 0 ? 0 : SuppressSmallCount((int)Math.Round(sessions.Count * 100m / allSessions.Count), sessionContributors, minimum),
                totalCompletedWorkouts = SuppressSmallCount(sessions.Count, completedContributors, minimum),
                assessmentParticipation = SuppressSmallCount((int)Math.Round(assessmentContributors * 100m / players.Count), assessmentContributors, minimum),
                ageGroups,
                municipalities,
                skillDistribution,
                engagementTrend = trend,
                privacyNotice = $"Регион: {region}. Метрики менее {minimum} уникальных игроков подавлены; малые разрезы скрываются целиком. Ответ не содержит имён, email, дат рождения и идентификаторов игроков."
            });
        }).RequireAuthorization(Roles.RegionalAnalyst).WithTags("Regional analytics");
    }

    private static int? SuppressSmallCount(int value, int contributors, int minimum) => contributors == 0 || contributors >= minimum ? value : null;
    private static string AgeBand(int age) => age switch { < 10 => "до 10", < 12 => "10–11", < 14 => "12–13", < 16 => "14–15", < 18 => "16–17", _ => "18+" };
}
