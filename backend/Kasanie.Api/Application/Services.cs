using System.Security.Claims;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Application;

public static class AgePolicy
{
    public static int GetAge(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return age;
    }

    public static bool CanRegisterIndependently(DateOnly birthDate, DateOnly today) => GetAge(birthDate, today) >= 14;
}

public interface IAssessmentScorer
{
    int Calculate(decimal rawValue, AssessmentDefinition definition, AssessmentNorm norm);
}

public sealed class AssessmentScorer : IAssessmentScorer
{
    public int Calculate(decimal rawValue, AssessmentDefinition definition, AssessmentNorm norm)
    {
        var low = norm.LowPerformanceValue;
        var high = norm.HighPerformanceValue;
        if (low == high) return 50;
        var ratio = definition.ScoringDirection == ScoringDirection.HigherIsBetter
            ? (rawValue - low) / (high - low)
            : (low - rawValue) / (low - high);
        return Math.Clamp((int)Math.Round(ratio * 100m), 0, 100);
    }
}

public static class AssessmentResultCollection
{
    public static AssessmentResult Upsert(AssessmentSession session, int definitionId, decimal rawValue, int normalizedScore)
    {
        var result = session.Results.FirstOrDefault(x => x.AssessmentDefinitionId == definitionId);
        if (result is null)
        {
            result = new AssessmentResult { AssessmentDefinitionId = definitionId };
            session.Results.Add(result);
        }

        result.RawValue = rawValue;
        result.NormalizedScore = normalizedScore;
        return result;
    }
}

public interface ITrainingPlanGenerator
{
    TrainingPlan Generate(PlayerProfile player, SkillSnapshot snapshot, IReadOnlyList<Exercise> exercises, DateOnly weekStart);
}

public sealed class TrainingPlanGenerator : ITrainingPlanGenerator
{
    public TrainingPlan Generate(PlayerProfile player, SkillSnapshot snapshot, IReadOnlyList<Exercise> exercises, DateOnly weekStart)
    {
        var scores = Enum.GetValues<SkillCategory>().ToDictionary(x => x, snapshot.Get);
        var weakest = scores.OrderBy(x => x.Value).Take(2).Select(x => x.Key).ToArray();
        var positionSkill = player.PreferredPosition.ToLowerInvariant() switch
        {
            var p when p.Contains("врат") => SkillCategory.Agility,
            var p when p.Contains("защит") => SkillCategory.Passing,
            var p when p.Contains("напад") => SkillCategory.Shooting,
            _ => SkillCategory.BallControl
        };

        var ranked = exercises.Where(x => x.IsActive)
            .OrderByDescending(x => weakest.Contains(x.SkillCategory) ? 5 : x.SkillCategory == positionSkill ? 3 : 1)
            .ThenBy(x => x.Difficulty)
            .ThenBy(x => x.Id)
            .ToList();

        var plan = new TrainingPlan
        {
            PlayerId = player.Id,
            WeekStart = weekStart,
            GenerationReason = $"Приоритет: {string.Join(", ", weakest.Select(SkillNames.Russian))}; позиция: {player.PreferredPosition}"
        };

        for (var dayIndex = 0; dayIndex < 3; dayIndex++)
        {
            var day = new TrainingDay
            {
                PlannedDate = weekStart.AddDays(dayIndex * 2),
                Title = dayIndex switch { 0 => "Техника и скорость", 1 => "Игровая работа", _ => "Закрепление" }
            };
            var used = new HashSet<int>();
            foreach (var exercise in ranked.Skip(dayIndex).Concat(ranked).Where(x => used.Add(x.Id)).Take(4))
            {
                day.Exercises.Add(new TrainingExercise
                {
                    ExerciseId = exercise.Id,
                    SortOrder = day.Exercises.Count + 1,
                    TargetDurationMinutes = exercise.DurationMinutes
                });
            }
            plan.Days.Add(day);
        }
        return plan;
    }
}

public static class SkillNames
{
    public static string Russian(SkillCategory value) => value switch
    {
        SkillCategory.Speed => "Скорость",
        SkillCategory.Endurance => "Выносливость",
        SkillCategory.BallControl => "Контроль мяча",
        SkillCategory.Passing => "Передачи",
        SkillCategory.Shooting => "Удары",
        _ => "Ловкость"
    };
}

public interface IAccessService
{
    Task<PlayerProfile?> OwnPlayerAsync(ClaimsPrincipal user);
    Task<bool> CoachCanAccessAsync(ClaimsPrincipal user, int playerId);
    Task<bool> ParentCanAccessAsync(ClaimsPrincipal user, int playerId);
}

public sealed class AccessService(AppDbContext db) : IAccessService
{
    private static string? UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public Task<PlayerProfile?> OwnPlayerAsync(ClaimsPrincipal user) =>
        db.Players.Include(x => x.Municipality).SingleOrDefaultAsync(x => x.UserId == UserId(user));

    public Task<bool> CoachCanAccessAsync(ClaimsPrincipal user, int playerId)
    {
        var id = UserId(user);
        return db.CoachPlayerLinks.AnyAsync(x => x.PlayerId == playerId && x.Status == LinkStatus.Active && x.Coach.UserId == id);
    }

    public Task<bool> ParentCanAccessAsync(ClaimsPrincipal user, int playerId)
    {
        var id = UserId(user);
        return db.ParentPlayerLinks.AnyAsync(x => x.PlayerId == playerId && x.Parent.UserId == id);
    }
}

public interface IAuditService
{
    Task WriteAsync(string? userId, string eventType, string entityType, string? entityId = null, string? details = null);
}

public sealed class AuditService(AppDbContext db) : IAuditService
{
    public async Task WriteAsync(string? userId, string eventType, string entityType, string? entityId = null, string? details = null)
    {
        db.AuditLogs.Add(new AuditLog { UserId = userId, EventType = eventType, EntityType = entityType, EntityId = entityId, Details = details });
        await db.SaveChangesAsync();
    }
}

public static class Dates
{
    public static DateOnly Monday(DateOnly date)
    {
        var shift = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-shift);
    }
}
