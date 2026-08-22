using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Application;

public sealed record PlayerDevelopmentCategory(
    string Key,
    string Name,
    int Exercises,
    int CompletionRate,
    int UnderstandingRate,
    int AttentionCount);

public sealed record PlayerDevelopmentExercise(
    int ExerciseId,
    string Name,
    string SkillCategory,
    bool IsCompleted,
    bool Understood);

public sealed record PlayerDevelopmentTraining(
    int TrainingId,
    string Title,
    string Team,
    string School,
    DateTimeOffset ScheduledAt,
    string Attendance,
    int CompletionRate,
    int UnderstandingRate,
    IReadOnlyList<PlayerDevelopmentExercise> Exercises);

public sealed record PlayerDevelopmentTrendPoint(
    int TrainingId,
    DateTimeOffset Date,
    int CompletionRate,
    int UnderstandingRate);

public sealed record PlayerDevelopmentFocus(
    string SkillCategory,
    string Name,
    int AttentionCount,
    DateTimeOffset LastSeenAt);

public sealed record PlayerDevelopmentSummary(
    DateTimeOffset GeneratedAt,
    int CompletedTeamTrainings,
    int Attended,
    int Late,
    int Absent,
    int Excused,
    int AttendanceRate,
    int CompletionRate,
    int UnderstandingRate,
    IReadOnlyList<PlayerDevelopmentCategory> Categories,
    IReadOnlyList<PlayerDevelopmentFocus> FocusAreas,
    IReadOnlyList<PlayerDevelopmentTrendPoint> Trend,
    IReadOnlyList<PlayerDevelopmentTraining> RecentTrainings);

public interface IPlayerDevelopmentService
{
    Task<PlayerDevelopmentSummary> BuildAsync(int playerId);
}

public sealed class PlayerDevelopmentService(AppDbContext db) : IPlayerDevelopmentService
{
    public async Task<PlayerDevelopmentSummary> BuildAsync(int playerId)
    {
        var attendances = await db.TeamTrainingAttendances.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.TeamTraining.Status == TeamTrainingStatus.Completed)
            .OrderByDescending(x => x.TeamTraining.ScheduledAt)
            .Take(100)
            .Select(x => new AttendanceRow(
                x.TeamTrainingId,
                x.TeamTraining.Title,
                x.TeamTraining.Team.Name,
                x.TeamTraining.Team.School.Name,
                x.TeamTraining.ScheduledAt,
                x.Status))
            .ToListAsync();

        if (attendances.Count == 0) return Empty();

        var trainingIds = attendances.Select(x => x.TrainingId).ToList();
        var exercises = await db.TeamTrainingExercises.AsNoTracking()
            .Where(x => trainingIds.Contains(x.TeamTrainingId))
            .OrderBy(x => x.TeamTrainingId)
            .ThenBy(x => x.SortOrder)
            .Select(x => new ExerciseRow(
                x.TeamTrainingId,
                x.ExerciseId,
                x.Exercise.Name,
                x.Exercise.SkillCategory,
                x.PlayerResults.Where(r => r.PlayerId == playerId).Select(r => (bool?)r.IsCompleted).FirstOrDefault(),
                x.PlayerResults.Where(r => r.PlayerId == playerId).Select(r => (bool?)r.Understood).FirstOrDefault()))
            .ToListAsync();

        var attendedRows = attendances.Where(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late).ToList();
        var attendedTrainingIds = attendedRows.Select(x => x.TrainingId).ToHashSet();
        var resultRows = exercises.Where(x => attendedTrainingIds.Contains(x.TrainingId) && x.IsCompleted.HasValue && x.Understood.HasValue).ToList();
        var byTraining = resultRows.GroupBy(x => x.TrainingId).ToDictionary(x => x.Key, x => x.ToList());

        var categoryMetrics = resultRows.GroupBy(x => x.SkillCategory).Select(group =>
        {
            var rows = group.ToList();
            return new PlayerDevelopmentCategory(
                group.Key.ToString(),
                SkillNames.Russian(group.Key),
                rows.Count,
                Percent(rows.Count(x => x.IsCompleted == true), rows.Count),
                Percent(rows.Count(x => x.Understood == true), rows.Count),
                rows.Count(x => x.IsCompleted != true || x.Understood != true));
        }).OrderByDescending(x => x.AttentionCount).ThenBy(x => x.Name).ToList();

        var dateByTraining = attendances.ToDictionary(x => x.TrainingId, x => x.ScheduledAt);
        var focusAreas = resultRows.Where(x => x.IsCompleted != true || x.Understood != true)
            .GroupBy(x => x.SkillCategory)
            .Select(group => new PlayerDevelopmentFocus(
                group.Key.ToString(),
                SkillNames.Russian(group.Key),
                group.Count(),
                group.Max(x => dateByTraining[x.TrainingId])))
            .OrderByDescending(x => x.AttentionCount)
            .ThenByDescending(x => x.LastSeenAt)
            .Take(3)
            .ToList();

        var trainingRows = attendances.Select(attendance =>
        {
            var rows = byTraining.GetValueOrDefault(attendance.TrainingId) ?? [];
            var items = rows.Select(x => new PlayerDevelopmentExercise(
                x.ExerciseId,
                x.Name,
                x.SkillCategory.ToString(),
                x.IsCompleted == true,
                x.Understood == true)).ToList();
            return new PlayerDevelopmentTraining(
                attendance.TrainingId,
                attendance.Title,
                attendance.Team,
                attendance.School,
                attendance.ScheduledAt,
                attendance.Status.ToString(),
                Percent(rows.Count(x => x.IsCompleted == true), rows.Count),
                Percent(rows.Count(x => x.Understood == true), rows.Count),
                items);
        }).ToList();

        var trend = trainingRows.Where(x => x.Attendance is nameof(AttendanceStatus.Present) or nameof(AttendanceStatus.Late))
            .Take(12)
            .OrderBy(x => x.ScheduledAt)
            .Select(x => new PlayerDevelopmentTrendPoint(x.TrainingId, x.ScheduledAt, x.CompletionRate, x.UnderstandingRate))
            .ToList();
        var attendanceDenominator = attendances.Count(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.Absent);

        return new PlayerDevelopmentSummary(
            DateTimeOffset.UtcNow,
            attendances.Count,
            attendedRows.Count,
            attendances.Count(x => x.Status == AttendanceStatus.Late),
            attendances.Count(x => x.Status == AttendanceStatus.Absent),
            attendances.Count(x => x.Status == AttendanceStatus.Excused),
            Percent(attendedRows.Count, attendanceDenominator),
            Percent(resultRows.Count(x => x.IsCompleted == true), resultRows.Count),
            Percent(resultRows.Count(x => x.Understood == true), resultRows.Count),
            categoryMetrics,
            focusAreas,
            trend,
            trainingRows.Take(10).ToList());
    }

    private static int Percent(int value, int total) => total == 0 ? 0 : (int)Math.Round(value * 100m / total);

    private static PlayerDevelopmentSummary Empty() => new(
        DateTimeOffset.UtcNow, 0, 0, 0, 0, 0, 0, 0, 0, [], [], [], []);

    private sealed record AttendanceRow(int TrainingId, string Title, string Team, string School, DateTimeOffset ScheduledAt, AttendanceStatus Status);
    private sealed record ExerciseRow(int TrainingId, int ExerciseId, string Name, SkillCategory SkillCategory, bool? IsCompleted, bool? Understood);
}
