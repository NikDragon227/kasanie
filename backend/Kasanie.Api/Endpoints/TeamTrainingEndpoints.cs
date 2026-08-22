using System.Security.Claims;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapTeamTraining(this IEndpointRouteBuilder app)
    {
        var journal = app.MapGroup("/api/coach/team-trainings").RequireAuthorization(Roles.Coach).WithTags("Team training journal");

        journal.MapGet("/", async (int? teamId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = db.TeamTrainings.AsNoTracking().Where(x => x.Team.TeamCoaches.Any(c => c.Coach.UserId == userId) && x.Team.IsActive && x.Team.School.IsActive);
            if (teamId.HasValue) query = query.Where(x => x.TeamId == teamId.Value);
            return Results.Ok(await query.OrderByDescending(x => x.ScheduledAt).Take(100).Select(x => new
            {
                x.Id, x.TeamId, team = (x.Team.AgeGroup ?? "") + (x.Team.AgeGroup == null ? "" : " — ") + x.Team.Name, school = x.Team.School.Name, x.Title, x.ScheduledAt,
                status = x.Status.ToString(), x.CompletedAt,
                players = x.Attendances.Count,
                present = x.Attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late),
                attention = x.Exercises.SelectMany(e => e.PlayerResults).Count(r => !r.IsCompleted || !r.Understood),
                exercises = x.Exercises.Count
            }).ToListAsync());
        });

        journal.MapPost("/", async (CreateTeamTrainingRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Укажите название тренировки."] });
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await db.TeamCoaches.Where(x => x.TeamId == request.TeamId && x.Team.IsActive && x.Team.School.IsActive && x.Coach.UserId == userId).Select(x => x.Coach).SingleOrDefaultAsync();
            if (coach is null) return Results.Forbid();
            var exerciseIds = request.ExerciseIds.Distinct().ToList();
            if (exerciseIds.Count is < 1 or > 8) return Results.ValidationProblem(new Dictionary<string, string[]> { ["exerciseIds"] = ["Выберите от 1 до 8 упражнений."] });
            var activeExerciseIds = await db.Exercises.Where(x => exerciseIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync();
            if (activeExerciseIds.Count != exerciseIds.Count) return Results.ValidationProblem(new Dictionary<string, string[]> { ["exerciseIds"] = ["Одно из упражнений недоступно."] });
            var playerIds = await db.TeamPlayers.Where(x => x.TeamId == request.TeamId && x.IsActive).OrderBy(x => x.Player.LastName).Select(x => x.PlayerId).ToListAsync();
            if (playerIds.Count == 0) return Results.UnprocessableEntity(new { message = "Сначала добавьте игроков в состав команды." });

            var training = new TeamTraining { TeamId = request.TeamId, CoachId = coach.Id, Title = request.Title.Trim(), ScheduledAt = request.ScheduledAt };
            for (var i = 0; i < exerciseIds.Count; i++) training.Exercises.Add(new TeamTrainingExercise { ExerciseId = exerciseIds[i], SortOrder = i + 1 });
            foreach (var playerId in playerIds) training.Attendances.Add(new TeamTrainingAttendance { PlayerId = playerId });
            db.TeamTrainings.Add(training); await db.SaveChangesAsync();
            db.AuditLogs.Add(new AuditLog { UserId = userId, EventType = "team_training_created", EntityType = nameof(TeamTraining), EntityId = training.Id.ToString(), Details = $"team={request.TeamId}" }); await db.SaveChangesAsync();
            return Results.Created($"/api/coach/team-trainings/{training.Id}", new { training.Id });
        });

        journal.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CoachCanAccessTrainingAsync(db, principal, id)) return Results.Forbid();
            var training = await db.TeamTrainings.AsNoTracking().Include(x => x.Team).ThenInclude(x => x.School).Include(x => x.Coach)
                .Include(x => x.Exercises).ThenInclude(x => x.Exercise)
                .Include(x => x.Exercises).ThenInclude(x => x.PlayerResults)
                .Include(x => x.Attendances).ThenInclude(x => x.Player)
                .SingleAsync(x => x.Id == id);
            return Results.Ok(new
            {
                training.Id, training.TeamId, team = (training.Team.AgeGroup ?? "") + (training.Team.AgeGroup == null ? "" : " — ") + training.Team.Name, school = training.Team.School.Name, training.Title, training.ScheduledAt,
                status = training.Status.ToString(), training.Notes, training.AttendanceSavedAt, training.CompletedAt,
                exercises = training.Exercises.OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.ExerciseId, x.Exercise.Name, skillCategory = x.Exercise.SkillCategory.ToString(), x.Exercise.DurationMinutes }),
                players = training.Attendances.OrderBy(x => x.Player.LastName).ThenBy(x => x.Player.FirstName).Select(x => new
                {
                    x.PlayerId, x.Player.FirstName, x.Player.LastName, attendance = x.Status.ToString(),
                    results = training.Exercises.OrderBy(e => e.SortOrder).Select(e =>
                    {
                        var result = e.PlayerResults.FirstOrDefault(r => r.PlayerId == x.PlayerId);
                        return new { teamTrainingExerciseId = e.Id, isCompleted = result?.IsCompleted, understood = result?.Understood };
                    })
                })
            });
        });

        journal.MapPut("/{id:int}/attendance", async (int id, SaveTeamAttendanceRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CoachCanAccessTrainingAsync(db, principal, id)) return Results.Forbid();
            var training = await db.TeamTrainings.Include(x => x.Attendances).Include(x => x.Exercises).ThenInclude(x => x.PlayerResults).SingleAsync(x => x.Id == id);
            if (training.Status == TeamTrainingStatus.Completed) return Results.Conflict(new { message = "Завершённую тренировку нельзя менять." });
            var expected = training.Attendances.Select(x => x.PlayerId).ToHashSet();
            var actual = request.Players.Select(x => x.PlayerId).ToList();
            if (actual.Count != actual.Distinct().Count() || !expected.SetEquals(actual)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["players"] = ["Передайте посещаемость для всего состава этой тренировки."] });
            var parsed = request.Players.Select(x => new { x.PlayerId, valid = Enum.TryParse<AttendanceStatus>(x.Status, true, out var status), status }).ToList();
            if (parsed.Any(x => !x.valid || x.status == AttendanceStatus.Unknown)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["attendance"] = ["Для каждого игрока выберите статус посещения."] });
            foreach (var item in parsed) training.Attendances.Single(x => x.PlayerId == item.PlayerId).Status = item.status;
            var presentPlayerIds = parsed.Where(x => x.status is AttendanceStatus.Present or AttendanceStatus.Late).Select(x => x.PlayerId).ToHashSet();
            db.TeamTrainingPlayerResults.RemoveRange(training.Exercises.SelectMany(x => x.PlayerResults).Where(x => !presentPlayerIds.Contains(x.PlayerId)));
            training.AttendanceSavedAt = DateTimeOffset.UtcNow; training.Status = TeamTrainingStatus.InProgress;
            db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "team_training_attendance_saved", EntityType = nameof(TeamTraining), EntityId = id.ToString() });
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        journal.MapPut("/{id:int}/review", async (int id, SaveTeamTrainingReviewRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CoachCanAccessTrainingAsync(db, principal, id)) return Results.Forbid();
            var training = await db.TeamTrainings.Include(x => x.Attendances).Include(x => x.Exercises).ThenInclude(x => x.PlayerResults).SingleAsync(x => x.Id == id);
            if (training.Status == TeamTrainingStatus.Completed) return Results.Conflict(new { message = "Завершённую тренировку нельзя менять." });
            if (training.Attendances.Any(x => x.Status == AttendanceStatus.Unknown)) return Results.UnprocessableEntity(new { message = "Сначала сохраните посещаемость." });
            var presentPlayers = training.Attendances.Where(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late).Select(x => x.PlayerId).ToHashSet();
            var exerciseIds = training.Exercises.Select(x => x.Id).ToHashSet();
            if (request.Results.GroupBy(x => new { x.PlayerId, x.TeamTrainingExerciseId }).Any(x => x.Count() > 1) || request.Results.Any(x => !presentPlayers.Contains(x.PlayerId) || !exerciseIds.Contains(x.TeamTrainingExerciseId)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["results"] = ["Отметки должны относиться к присутствующим игрокам и упражнениям этой тренировки."] });
            foreach (var item in request.Results)
            {
                var exercise = training.Exercises.Single(x => x.Id == item.TeamTrainingExerciseId);
                var result = exercise.PlayerResults.SingleOrDefault(x => x.PlayerId == item.PlayerId);
                if (result is null) exercise.PlayerResults.Add(new TeamTrainingPlayerResult { PlayerId = item.PlayerId, IsCompleted = item.IsCompleted, Understood = item.Understood });
                else { result.IsCompleted = item.IsCompleted; result.Understood = item.Understood; result.UpdatedAt = DateTimeOffset.UtcNow; }
            }
            training.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        journal.MapPost("/{id:int}/complete", async (int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CoachCanAccessTrainingAsync(db, principal, id)) return Results.Forbid();
            var training = await db.TeamTrainings.Include(x => x.Attendances).Include(x => x.Exercises).ThenInclude(x => x.PlayerResults).SingleAsync(x => x.Id == id);
            if (training.Status == TeamTrainingStatus.Completed) return Results.Ok(new { training.Id, training.CompletedAt });
            if (training.Attendances.Any(x => x.Status == AttendanceStatus.Unknown)) return Results.UnprocessableEntity(new { message = "Заполните посещаемость для всей команды." });
            var presentPlayers = training.Attendances.Count(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late);
            var expectedResults = presentPlayers * training.Exercises.Count;
            var presentPlayerIds = training.Attendances.Where(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late).Select(x => x.PlayerId).ToHashSet();
            var actualResults = training.Exercises.Sum(x => x.PlayerResults.Count(r => presentPlayerIds.Contains(r.PlayerId)));
            if (actualResults != expectedResults) return Results.UnprocessableEntity(new { message = "Для каждого присутствующего игрока отметьте выполнение и понимание всех упражнений." });
            training.Status = TeamTrainingStatus.Completed; training.CompletedAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "team_training_completed", EntityType = nameof(TeamTraining), EntityId = id.ToString(), Details = $"present={presentPlayers};attention={training.Exercises.Sum(x => x.PlayerResults.Count(r => !r.IsCompleted || !r.Understood))}" });
            await db.SaveChangesAsync(); return Results.Ok(new { training.Id, training.CompletedAt });
        });
    }

    private static Task<bool> CoachCanAccessTrainingAsync(AppDbContext db, ClaimsPrincipal principal, int trainingId)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return db.TeamTrainings.AnyAsync(x => x.Id == trainingId && x.Team.IsActive && x.Team.School.IsActive && x.Team.TeamCoaches.Any(c => c.Coach.UserId == userId));
    }
}
