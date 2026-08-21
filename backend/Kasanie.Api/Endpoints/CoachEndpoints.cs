using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapCoach(this IEndpointRouteBuilder app)
    {
        var coach = app.MapGroup("/api/coach").RequireAuthorization(Roles.Coach).WithTags("Coach");
        coach.MapGet("/catalog", async (AppDbContext db) => Results.Ok(new
        {
            exercises = await db.Exercises.AsNoTracking().Where(x => x.IsActive && !x.Name.StartsWith("E2E ") && !x.Name.StartsWith("Smoke CRUD ")).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, skillCategory = x.SkillCategory.ToString(), x.DurationMinutes }).ToListAsync(),
            programs = await db.TrainingPrograms.AsNoTracking().Where(x => x.IsActive && !x.Name.StartsWith("E2E ")).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Description }).ToListAsync()
        }));
        coach.MapGet("/players", async (string? search, string? level, ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = db.CoachPlayerLinks.AsNoTracking().Where(x => x.Coach.UserId == userId && x.Status == LinkStatus.Active).Select(x => x.Player);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(search.ToLower()));
            if (!string.IsNullOrWhiteSpace(level)) query = query.Where(x => x.ExperienceLevel == level);
            var players = await query.OrderBy(x => x.LastName).Select(x => new
            {
                x.Id, x.FirstName, x.LastName, x.PreferredPosition, x.ExperienceLevel,
                lastActivity = db.TrainingSessions.Where(s => s.PlayerId == x.Id).Max(s => (DateTimeOffset?)s.CompletedAt),
                completed = db.TrainingSessions.Count(s => s.PlayerId == x.Id && s.Status == SessionStatus.Completed),
                planned = db.TrainingSessions.Count(s => s.PlayerId == x.Id)
            }).ToListAsync();
            return Results.Ok(players.Select(x => new { x.Id, x.FirstName, x.LastName, x.PreferredPosition, x.ExperienceLevel, x.lastActivity, planCompletion = x.planned == 0 ? 0 : (int)Math.Round(x.completed * 100m / x.planned) }));
        });

        coach.MapGet("/players/{playerId:int}", async (int playerId, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            if (!await access.CoachCanAccessAsync(user, playerId)) return Results.Forbid();
            var player = await db.Players.AsNoTracking().Include(x => x.Municipality).SingleAsync(x => x.Id == playerId);
            var skills = await db.SkillSnapshots.AsNoTracking().Where(x => x.PlayerId == playerId).OrderByDescending(x => x.CapturedAt).Take(12).ToListAsync();
            var plan = await db.TrainingPlans.AsNoTracking().Where(x => x.PlayerId == playerId && x.Status == PlanStatus.Active).Include(x => x.Days).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).OrderByDescending(x => x.WeekStart).FirstOrDefaultAsync();
            var sessions = await db.TrainingSessions.AsNoTracking().Where(x => x.PlayerId == playerId).OrderByDescending(x => x.CompletedAt).Take(20).Select(x => new
            {
                x.Id, x.Status, x.StartedAt, x.CompletedAt, x.TrainingDay.Title, x.Notes,
                feedback = x.Results.Where(result => result.PerceivedDifficulty != null || !string.IsNullOrWhiteSpace(result.Notes)).Select(result => new { name = result.TrainingExercise.Exercise.Name, result.PerceivedDifficulty, result.Notes })
            }).ToListAsync();
            var notes = await db.CoachNotes.AsNoTracking().Where(x => x.PlayerId == playerId).OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync();
            return Results.Ok(new { profile = PlayerDto(player), skillHistory = skills.Select(x => new { x.CapturedAt, skills = SkillsDto(x) }), plan = plan is null ? null : PlanDto(plan, []), sessions, notes = notes.Select(x => new { x.Id, x.Text, x.CreatedAt }) });
        });

        coach.MapPost("/players/{playerId:int}/notes", async (int playerId, CoachNoteRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            if (!await access.CoachCanAccessAsync(user, playerId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Text)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Заметка не может быть пустой."] });
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var coachProfile = await db.CoachProfiles.SingleAsync(x => x.UserId == userId);
            var note = new CoachNote { CoachId = coachProfile.Id, PlayerId = playerId, Text = request.Text.Trim() };
            db.CoachNotes.Add(note); await db.SaveChangesAsync();
            return Results.Created($"/api/coach/players/{playerId}", new { note.Id, note.Text, note.CreatedAt });
        });

        coach.MapPost("/players/{playerId:int}/plan/exercises", async (int playerId, AddPlanExerciseRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            if (!await access.CoachCanAccessAsync(user, playerId)) return Results.Forbid();
            var day = await db.TrainingDays.Include(x => x.TrainingPlan).Include(x => x.Exercises).FirstOrDefaultAsync(x => x.Id == request.TrainingDayId && x.TrainingPlan.PlayerId == playerId && x.TrainingPlan.Status == PlanStatus.Active);
            var exercise = await db.Exercises.FindAsync(request.ExerciseId);
            if (day is null || exercise is null || !exercise.IsActive) return Results.NotFound();
            day.Exercises.Add(new TrainingExercise { ExerciseId = exercise.Id, SortOrder = day.Exercises.Count + 1, TargetDurationMinutes = exercise.DurationMinutes });
            db.AuditLogs.Add(new AuditLog { UserId = user.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "coach_plan_exercise_added", EntityType = nameof(PlayerProfile), EntityId = playerId.ToString() });
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        coach.MapPut("/players/{playerId:int}/plan/exercises", async (int playerId, ReplacePlanExerciseRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            if (!await access.CoachCanAccessAsync(user, playerId)) return Results.Forbid();
            var item = await db.TrainingExercises.Include(x => x.TrainingDay).ThenInclude(x => x.TrainingPlan).FirstOrDefaultAsync(x => x.Id == request.TrainingExerciseId && x.TrainingDay.TrainingPlan.PlayerId == playerId && x.TrainingDay.TrainingPlan.Status == PlanStatus.Active);
            var exercise = await db.Exercises.FindAsync(request.ExerciseId);
            if (item is null || exercise is null || !exercise.IsActive) return Results.NotFound();
            item.ExerciseId = exercise.Id; item.TargetDurationMinutes = exercise.DurationMinutes;
            db.AuditLogs.Add(new AuditLog { UserId = user.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "coach_plan_exercise_replaced", EntityType = nameof(TrainingExercise), EntityId = item.Id.ToString() });
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        coach.MapPost("/players/{playerId:int}/program", async (int playerId, AssignProgramRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            if (!await access.CoachCanAccessAsync(user, playerId)) return Results.Forbid();
            var program = await db.TrainingPrograms.FindAsync(request.TrainingProgramId); if (program is null || !program.IsActive) return Results.NotFound();
            var plan = await db.TrainingPlans.Where(x => x.PlayerId == playerId && x.Status == PlanStatus.Active).OrderByDescending(x => x.WeekStart).FirstOrDefaultAsync();
            if (plan is null) return Results.Problem("Сначала игроку нужен активный план.", statusCode: 422);
            plan.TrainingProgramId = program.Id; plan.GenerationReason = $"Назначено тренером: {program.Name}"; await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
