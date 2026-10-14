using System.Security.Claims;
using System.Text.Json;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static readonly JsonSerializerOptions TrainingJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] TrainingCategories = ["General", "Defence", "Attack", "Possession", "Tactics", "Fitness", "Goalkeeping", "SetPieces", "Recovery", "Rest"];
    private static readonly string[] TrainingGroups = ["Goalkeepers", "Defence", "Attack"];
    private static readonly string[] TrainingIntensities = ["Normal", "Half", "Double", "Rest"];
    private static readonly string[] TrainingTasks = ["GkShots", "GkHandling", "DefenceTactics", "DefenceTechnique", "AttackTactics", "AttackTechnique", "PossessionTactics", "PossessionTechnique", "Strength", "Speed"];

    private static void MapTrainingWorkspace(RouteGroupBuilder coach)
    {
        coach.MapGet("/teams/{teamId:int}/training-workspace", async (int teamId, int? year, int? utcOffsetMinutes, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId, false);
            if (team is null) return Results.Forbid();
            var selectedYear = year ?? DateTime.UtcNow.Year;
            var offset = utcOffsetMinutes ?? 0;
            if (selectedYear is < 2000 or > 2100 || offset is < -840 or > 840) return Results.BadRequest(new { message = "Проверьте год и часовой пояс." });
            var row = await db.TeamTrainingWorkspaces.AsNoTracking().SingleOrDefaultAsync(x => x.TeamId == teamId);
            var state = ReadTrainingState(row);
            var coaches = await TrainingStaff(db, teamId, team.SchoolId);
            var from = new DateTimeOffset(selectedYear, 1, 1, 0, 0, 0, TimeSpan.FromMinutes(offset)).AddDays(-7).ToUniversalTime();
            var to = from.AddYears(1).AddDays(14);
            var locked = await TrainingLocks(db, teamId, offset, from, to);
            var localNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(offset));
            if (selectedYear != localNow.Year)
                locked.AddRange(await TrainingLocks(db, teamId, offset, localNow.AddDays(-7).ToUniversalTime(), localNow.AddDays(7).ToUniversalTime()));
            return Results.Ok(new { state, version = row?.Version ?? 0, row?.UpdatedAt, coaches, lockedSlots = locked });
        });

        coach.MapPut("/teams/{teamId:int}/training-workspace", async (int teamId, SaveTrainingWorkspaceRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId, false);
            if (team is null) return Results.Forbid();
            if (request.State is null || request.Version < 0 || request.UtcOffsetMinutes is < -840 or > 840)
                return Results.BadRequest(new { message = "Проверьте план и часовой пояс." });
            var state = request.State;
            var staff = await TrainingStaff(db, teamId, team.SchoolId);
            var playerIds = await db.TeamPlayers.Where(x => x.TeamId == teamId && x.IsActive).Select(x => x.PlayerId).ToListAsync();
            var errors = TrainingStateErrors(state, playerIds.ToHashSet(), staff.Select(x => x.Id).ToHashSet());
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var row = await db.TeamTrainingWorkspaces.SingleOrDefaultAsync(x => x.TeamId == teamId);
            if ((row?.Version ?? 0) != request.Version) return TrainingVersionConflict();
            var previous = ReadTrainingState(row);
            var previousCalendar = previous.Calendar.ToDictionary(x => (x.Date, x.Slot));
            var nextCalendar = state.Calendar.ToDictionary(x => (x.Date, x.Slot));
            var changed = previousCalendar.Keys.Union(nextCalendar.Keys).Where(key => previousCalendar.GetValueOrDefault(key) != nextCalendar.GetValueOrDefault(key)).ToHashSet();
            if (changed.Count > 0)
            {
                var offset = TimeSpan.FromMinutes(request.UtcOffsetMinutes);
                var from = new DateTimeOffset(changed.Min(x => x.Date).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
                var to = new DateTimeOffset(changed.Max(x => x.Date).AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
                var locks = await TrainingLocks(db, teamId, request.UtcOffsetMinutes, from, to);
                if (locks.Any(x => changed.Contains((x.Date, x.Slot))))
                    return Results.Conflict(new { message = "Слот занят матчем или занятием из журнала. Обновите календарь; занятие редактируется в журнале." });
            }
            if (row is null) { row = new TeamTrainingWorkspace { TeamId = teamId }; db.TeamTrainingWorkspaces.Add(row); }
            row.StateJson = JsonSerializer.Serialize(state, TrainingJson);
            row.Version++;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "training_workspace_saved", EntityType = nameof(TeamTrainingWorkspace), EntityId = teamId.ToString() });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TrainingVersionConflict(); }
            catch (DbUpdateException) when (request.Version == 0) { return TrainingVersionConflict(); }
            return Results.Ok(new { row.Version, row.UpdatedAt });
        });
    }

    private static IResult TrainingVersionConflict() => Results.Conflict(new { message = "План уже изменён другим тренером. Скопируйте свои изменения и обновите страницу перед сохранением." });
    private static TrainingWorkspaceState ReadTrainingState(TeamTrainingWorkspace? row) => row is null ? new() : JsonSerializer.Deserialize<TrainingWorkspaceState>(row.StateJson, TrainingJson) ?? new();

    private sealed record TrainingStaffItem(int Id, string Name, bool IsHeadCoach);
    private static async Task<List<TrainingStaffItem>> TrainingStaff(AppDbContext db, int teamId, int schoolId)
    {
        return await db.CoachProfiles.AsNoTracking()
            .Where(x => db.TeamCoaches.Any(c => c.TeamId == teamId && c.CoachId == x.Id) || db.SchoolMemberships.Any(m => m.SchoolId == schoolId && m.UserId == x.UserId && m.IsActive))
            .Select(x => new TrainingStaffItem(x.Id, x.DisplayName, db.TeamCoaches.Any(c => c.TeamId == teamId && c.CoachId == x.Id && c.IsHeadCoach)))
            .ToListAsync();
    }

    private sealed record TrainingCalendarLock(DateOnly Date, int Slot, string Kind, string Title, int Id, string Status, int Minutes);
    private static async Task<List<TrainingCalendarLock>> TrainingLocks(AppDbContext db, int teamId, int offsetMinutes, DateTimeOffset from, DateTimeOffset to)
    {
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var matches = await db.TeamMatches.AsNoTracking().Where(x => x.TeamId == teamId && x.ScheduledAt >= from && x.ScheduledAt < to && x.Status != "Отменён" && x.Status != "Отменен").OrderBy(x => x.ScheduledAt).ToListAsync();
        var journal = await db.TeamTrainings.AsNoTracking().Where(x => x.TeamId == teamId && x.ScheduledAt >= from && x.ScheduledAt < to)
            .OrderBy(x => x.ScheduledAt).Select(x => new { x.Id, x.Title, x.ScheduledAt, x.Status, minutes = x.Exercises.Sum(e => e.Exercise.DurationMinutes) }).ToListAsync();
        var result = new List<TrainingCalendarLock>();
        foreach (var group in matches.GroupBy(x => DateOnly.FromDateTime(x.ScheduledAt.ToOffset(offset).DateTime)))
            result.Add(new(group.Key, 1, "Match", string.Join(" · ", group.Select(x => x.Opponent)), group.First().Id, group.First().Status, 90));
        foreach (var group in journal.GroupBy(x => DateOnly.FromDateTime(x.ScheduledAt.ToOffset(offset).DateTime)))
        {
            var available = Enumerable.Range(0, 3).Where(slot => !result.Any(x => x.Date == group.Key && x.Slot == slot)).ToList();
            var items = group.ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // More than three existing sessions remain visible as links below the week.
                result.Add(new(group.Key, i < available.Count ? available[i] : 3 + i, "Training", item.Title, item.Id, item.Status.ToString(), item.minutes));
            }
        }
        return result;
    }

    private static Dictionary<string, string[]> TrainingStateErrors(TrainingWorkspaceState state, HashSet<int> players, HashSet<int> coaches)
    {
        var errors = new Dictionary<string, string[]>();
        void Error(string key, string value) => errors[key] = [value];
        if (state.Calendar is null || state.Players is null || state.Mentoring is null || state.Coaches is null || state.Tactics is null)
        { Error("state", "План должен содержать все разделы."); return errors; }
        if (state.Calendar.Any(x => x is null) || state.Players.Any(x => x is null) || state.Mentoring.Any(x => x is null || x.PlayerIds is null) || state.Coaches.Any(x => x is null || x.Tasks is null) || state.Tactics.Any(x => x is null))
        { Error("state", "Проверьте элементы плана."); return errors; }
        if (state.ResponsibleCoachId.HasValue && !coaches.Contains(state.ResponsibleCoachId.Value)) Error("responsibleCoachId", "Выберите тренера этой школы.");
        if (state.Calendar.Count > 2500 || state.Calendar.Any(x => x is null || x.Date.Year is < 2000 or > 2100 || x.Slot is < 0 or > 2 || string.IsNullOrWhiteSpace(x.Session) || x.Session.Length > 120 || !TrainingCategories.Contains(x.Category) || x.Intensity is < 0 or > 100 || x.Minutes is < 0 or > 240 || !new[] { "Team", "Goalkeepers", "Defence", "Attack" }.Contains(x.Group) || x.Notes?.Length > 2000) || state.Calendar.GroupBy(x => (x.Date, x.Slot)).Any(x => x.Count() > 1))
            Error("calendar", "Проверьте даты, слоты, нагрузку и продолжительность занятий.");
        if (state.Players.Count > 500 || state.Players.Any(x => x is null || !players.Contains(x.PlayerId) || !TrainingGroups.Contains(x.Group) || string.IsNullOrWhiteSpace(x.Position) || x.Position.Length > 80 || x.Role is null || x.Role.Length > 100 || x.Focus is null || x.Focus.Length > 120 || !TrainingIntensities.Contains(x.Intensity) || x.RestUntil?.Year is < 2000 or > 2100 || x.Notes?.Length > 2000) || state.Players.GroupBy(x => x.PlayerId).Any(x => x.Count() > 1))
            Error("players", "Индивидуальные настройки должны относиться к текущему составу. Укажите позицию, группу и нагрузку.");
        if (state.Mentoring.Count > 20 || state.Mentoring.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 80 || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 80 || x.PlayerIds is null || x.PlayerIds.Any(id => !players.Contains(id)) || x.MentorId.HasValue && !x.PlayerIds.Contains(x.MentorId.Value)) || state.Mentoring.GroupBy(x => x.Id).Any(x => x.Count() > 1) || state.Mentoring.SelectMany(x => x.PlayerIds).GroupBy(x => x).Any(x => x.Count() > 1))
            Error("mentoring", "Игрок может состоять в одной группе; наставник должен входить в её состав.");
        if (state.Coaches.Count > 100 || state.Coaches.Any(x => x is null || !coaches.Contains(x.CoachId) || x.Tasks is null || x.Tasks.Any(task => !TrainingTasks.Contains(task)) || x.Tasks.Distinct().Count() != x.Tasks.Count) || state.Coaches.GroupBy(x => x.CoachId).Any(x => x.Count() > 1))
            Error("coaches", "Назначайте направления только тренерам этой школы.");
        if (state.Tactics.Count > 3 || state.Tactics.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 100 || string.IsNullOrWhiteSpace(x.Formation) || x.Formation.Length > 80 || x.Notes?.Length > 2000)) Error("tactics", "Можно тренировать до трёх тактик. Укажите название и схему.");
        if (state.Satisfaction?.Length > 2000 || state.MatchPreparation?.Length > 4000 || state.LastMatchComment?.Length > 4000) Error("notes", "Сократите комментарий.");
        return errors;
    }
}
