using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapPlayer(this IEndpointRouteBuilder app)
    {
        var playerApi = app.MapGroup("/api/player").RequireAuthorization(Roles.Player).WithTags("Player");

        playerApi.MapGet("/dashboard", async (ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user);
            if (player is null) return Results.NotFound();
            var latest = await db.SkillSnapshots.AsNoTracking().Where(x => x.PlayerId == player.Id).OrderByDescending(x => x.CapturedAt).FirstOrDefaultAsync();
            var plan = await db.TrainingPlans.AsNoTracking().Where(x => x.PlayerId == player.Id && x.Status == PlanStatus.Active)
                .OrderByDescending(x => x.WeekStart).Include(x => x.Days).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).FirstOrDefaultAsync();
            var completed = await db.TrainingSessions.CountAsync(x => x.PlayerId == player.Id && x.Status == SessionStatus.Completed && x.CompletedAt >= DateTimeOffset.UtcNow.AddDays(-7));
            var total = plan?.Days.Count ?? 0;
            var achievements = await db.PlayerAchievements.AsNoTracking().Where(x => x.PlayerId == player.Id).OrderByDescending(x => x.AwardedAt).Take(3)
                .Select(x => new { x.AchievementDefinition.Name, x.AchievementDefinition.Description, x.AwardedAt }).ToListAsync();
            var nextDay = plan?.Days.Where(x => x.PlannedDate >= DateOnly.FromDateTime(DateTime.UtcNow)).OrderBy(x => x.PlannedDate).FirstOrDefault() ?? plan?.Days.OrderByDescending(x => x.PlannedDate).FirstOrDefault();
            return Results.Ok(new
            {
                profile = PlayerDto(player),
                level = latest is null ? 0 : new[] { latest.Speed, latest.Endurance, latest.BallControl, latest.Passing, latest.Shooting, latest.Agility }.Average(),
                weakestSkills = latest is null ? Array.Empty<object>() : Enum.GetValues<SkillCategory>().OrderBy(latest.Get).Take(2).Select(x => (object)new { key = x.ToString(), name = SkillNames.Russian(x), score = latest.Get(x) }).ToArray(),
                weeklyCompletion = total == 0 ? 0 : (int)Math.Round(completed * 100m / total),
                nextWorkout = nextDay is null ? null : new { nextDay.Id, nextDay.Title, nextDay.PlannedDate, duration = nextDay.Exercises.Sum(x => x.TargetDurationMinutes) },
                plan = plan is null ? null : PlanDto(plan, []),
                achievements,
                skills = latest is null ? null : SkillsDto(latest)
            });
        });

        playerApi.MapGet("/profile", async (ClaimsPrincipal user, IAccessService access) =>
        {
            var player = await access.OwnPlayerAsync(user);
            return player is null ? Results.NotFound() : Results.Ok(PlayerDto(player));
        });

        playerApi.MapGet("/development", async (ClaimsPrincipal user, IAccessService access, IPlayerDevelopmentService development) =>
        {
            var player = await access.OwnPlayerAsync(user);
            return player is null ? Results.NotFound() : Results.Ok(await development.BuildAsync(player.Id));
        });

        playerApi.MapPut("/profile", async (ProfileUpdateRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user);
            if (player is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Имя и фамилия обязательны."] });
            if (request.Height is < 80 or > 230 || request.Weight is < 20 or > 250) return Results.ValidationProblem(new Dictionary<string, string[]> { ["measurements"] = ["Проверьте рост и вес."] });
            var municipality = await ResolveCityAsync(db, request.City);
            if (municipality is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["city"] = ["Выберите город из подсказок."] });
            player.FirstName = request.FirstName.Trim(); player.LastName = request.LastName.Trim(); player.Gender = request.Gender;
            player.MunicipalityId = municipality.Id; player.Municipality = municipality; player.PreferredPosition = request.PreferredPosition; player.DominantFoot = request.DominantFoot;
            player.ExperienceLevel = request.ExperienceLevel; player.Height = request.Height; player.Weight = request.Weight; player.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(PlayerDto(player));
        });

        var assessments = app.MapGroup("/api/assessments").RequireAuthorization(Roles.Player).WithTags("Assessments");
        assessments.MapGet("/current", async (ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var definitions = await db.AssessmentDefinitions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder)
                .Select(x => new { x.Id, x.Name, x.Description, x.Instructions, x.Unit, skillCategory = x.SkillCategory.ToString(), x.MinimumReasonableValue, x.MaximumReasonableValue }).ToListAsync();
            var session = await db.AssessmentSessions.AsNoTracking().Include(x => x.Results).Where(x => x.PlayerId == player.Id && !x.IsCompleted).OrderByDescending(x => x.StartedAt).FirstOrDefaultAsync();
            return Results.Ok(new { definitions, session = session is null ? null : new { session.Id, session.StartedAt, values = session.Results.Select(x => new { definitionId = x.AssessmentDefinitionId, value = x.RawValue }) }, demoNotice = "Нормы являются демонстрационными и не считаются научно валидированными." });
        });

        assessments.MapPut("/draft", async (SubmitAssessmentRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var definitions = await db.AssessmentDefinitions.Where(x => x.IsActive).ToDictionaryAsync(x => x.Id);
            var invalid = request.Values.FirstOrDefault(x => !definitions.TryGetValue(x.DefinitionId, out var d) || x.Value < d.MinimumReasonableValue || x.Value > d.MaximumReasonableValue);
            if (invalid is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["values"] = ["Одно из значений находится вне допустимого диапазона."] });
            var session = await db.AssessmentSessions.Include(x => x.Results).FirstOrDefaultAsync(x => x.PlayerId == player.Id && !x.IsCompleted);
            if (session is null) { session = new AssessmentSession { PlayerId = player.Id }; db.AssessmentSessions.Add(session); }
            foreach (var value in request.Values)
            {
                var result = session.Results.FirstOrDefault(x => x.AssessmentDefinitionId == value.DefinitionId);
                if (result is null) session.Results.Add(new AssessmentResult { AssessmentDefinitionId = value.DefinitionId, RawValue = value.Value });
                else result.RawValue = value.Value;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { session.Id, saved = session.Results.Count });
        });

        assessments.MapPost("/submit", async (SubmitAssessmentRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db, IAssessmentScorer scorer, ITrainingPlanGenerator generator, IAuditService audit) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var definitions = await db.AssessmentDefinitions.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync();
            if (request.Values.Select(x => x.DefinitionId).Distinct().Count() != definitions.Count || definitions.Any(d => request.Values.All(x => x.DefinitionId != d.Id)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["values"] = ["Заполните все тесты."] });
            var age = AgePolicy.GetAge(player.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow));
            var norms = await db.AssessmentNorms.Where(x => x.MinimumAge <= age && x.MaximumAge >= age).ToDictionaryAsync(x => x.AssessmentDefinitionId);
            var session = await db.AssessmentSessions.Include(x => x.Results).FirstOrDefaultAsync(x => x.PlayerId == player.Id && !x.IsCompleted) ?? new AssessmentSession { PlayerId = player.Id };
            if (session.Id == 0) db.AssessmentSessions.Add(session);
            var activeDefinitionIds = definitions.Select(x => x.Id).ToHashSet();
            var obsoleteResults = session.Results.Where(x => !activeDefinitionIds.Contains(x.AssessmentDefinitionId)).ToList();
            db.AssessmentResults.RemoveRange(obsoleteResults);
            var scores = new Dictionary<SkillCategory, int>();
            foreach (var definition in definitions)
            {
                var raw = request.Values.Single(x => x.DefinitionId == definition.Id).Value;
                if (raw < definition.MinimumReasonableValue || raw > definition.MaximumReasonableValue) return Results.ValidationProblem(new Dictionary<string, string[]> { [definition.Id.ToString()] = [$"Допустимый диапазон: {definition.MinimumReasonableValue}–{definition.MaximumReasonableValue} {definition.Unit}."] });
                if (!norms.TryGetValue(definition.Id, out var norm)) return Results.Problem("Для возрастной группы не настроена демонстрационная шкала.", statusCode: 422);
                var score = scorer.Calculate(raw, definition, norm);
                scores[definition.SkillCategory] = score;
                AssessmentResultCollection.Upsert(session, definition.Id, raw, score);
            }
            session.IsCompleted = true; session.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            var snapshot = new SkillSnapshot { PlayerId = player.Id, AssessmentSessionId = session.Id, Speed = scores[SkillCategory.Speed], Endurance = scores[SkillCategory.Endurance], BallControl = scores[SkillCategory.BallControl], Passing = scores[SkillCategory.Passing], Shooting = scores[SkillCategory.Shooting], Agility = scores[SkillCategory.Agility] };
            db.SkillSnapshots.Add(snapshot);
            var oldPlans = await db.TrainingPlans.Where(x => x.PlayerId == player.Id && x.Status == PlanStatus.Active).ToListAsync();
            foreach (var old in oldPlans) old.Status = PlanStatus.Archived;
            var exerciseCatalog = await db.Exercises.AsNoTracking().Where(x => x.IsActive).ToListAsync();
            db.TrainingPlans.Add(generator.Generate(player, snapshot, exerciseCatalog, Dates.Monday(DateOnly.FromDateTime(DateTime.UtcNow))));
            if (!await db.PlayerAchievements.AnyAsync(x => x.PlayerId == player.Id && x.AchievementDefinition.Code == "FIRST_ASSESSMENT"))
            {
                var achievement = await db.AchievementDefinitions.FirstOrDefaultAsync(x => x.Code == "FIRST_ASSESSMENT");
                if (achievement is not null) db.PlayerAchievements.Add(new PlayerAchievement { PlayerId = player.Id, AchievementDefinitionId = achievement.Id });
            }
            await db.SaveChangesAsync();
            await audit.WriteAsync(user.FindFirstValue(ClaimTypes.NameIdentifier), "assessment_completed", nameof(AssessmentSession), session.Id.ToString());
            return Results.Ok(new { sessionId = session.Id, skills = SkillsDto(snapshot), weakest = scores.OrderBy(x => x.Value).Take(2).Select(x => new { name = SkillNames.Russian(x.Key), score = x.Value }), planGenerated = true });
        });

        assessments.MapGet("/history", async (ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var history = await db.SkillSnapshots.AsNoTracking().Where(x => x.PlayerId == player.Id).OrderByDescending(x => x.CapturedAt).Take(12).ToListAsync();
            return Results.Ok(history.Select(x => new { x.Id, x.CapturedAt, skills = SkillsDto(x) }));
        });

        var training = app.MapGroup("/api/training").RequireAuthorization(Roles.Player).WithTags("Training");
        training.MapGet("/plan", async (ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var plan = await db.TrainingPlans.AsNoTracking().Where(x => x.PlayerId == player.Id && x.Status == PlanStatus.Active).OrderByDescending(x => x.WeekStart)
                .Include(x => x.Days).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).FirstOrDefaultAsync();
            if (plan is null) return Results.Ok(null);
            var sessions = await db.TrainingSessions.AsNoTracking().Where(x => x.PlayerId == player.Id && plan.Days.Select(d => d.Id).Contains(x.TrainingDayId)).ToListAsync();
            return Results.Ok(PlanDto(plan, sessions));
        });

        training.MapPost("/days/{dayId:int}/start", async (int dayId, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var day = await db.TrainingDays.Include(x => x.TrainingPlan).Include(x => x.Exercises).FirstOrDefaultAsync(x => x.Id == dayId && x.TrainingPlan.PlayerId == player.Id);
            if (day is null) return Results.NotFound();
            var session = await db.TrainingSessions.Include(x => x.Results).FirstOrDefaultAsync(x => x.PlayerId == player.Id && x.TrainingDayId == dayId);
            if (session is null)
            {
                session = new TrainingSession { PlayerId = player.Id, TrainingDayId = dayId, Status = SessionStatus.InProgress, StartedAt = DateTimeOffset.UtcNow };
                foreach (var exercise in day.Exercises) session.Results.Add(new TrainingExerciseResult { TrainingExerciseId = exercise.Id });
                db.TrainingSessions.Add(session);
            }
            else if (session.Status == SessionStatus.Planned) { session.Status = SessionStatus.InProgress; session.StartedAt = DateTimeOffset.UtcNow; }
            await db.SaveChangesAsync();
            return Results.Ok(new { session.Id });
        });

        training.MapGet("/sessions/{sessionId:int}", async (int sessionId, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var session = await db.TrainingSessions.AsNoTracking().Include(x => x.TrainingDay).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).Include(x => x.Results)
                .FirstOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == player.Id);
            return session is null ? Results.NotFound() : Results.Ok(SessionDto(session));
        });

        training.MapPut("/sessions/{sessionId:int}/exercises/{trainingExerciseId:int}", async (int sessionId, int trainingExerciseId, ExerciseResultRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            if (request.PerceivedDifficulty is < 1 or > 5) return Results.ValidationProblem(new Dictionary<string, string[]> { ["perceivedDifficulty"] = ["Укажите значение от 1 до 5."] });
            var session = await db.TrainingSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == player.Id);
            if (session is null) return Results.NotFound();
            var result = await db.TrainingExerciseResults.FirstOrDefaultAsync(x => x.TrainingSessionId == sessionId && x.TrainingExerciseId == trainingExerciseId);
            if (result is null)
            {
                var belongsToSessionDay = await db.TrainingExercises.AnyAsync(x => x.Id == trainingExerciseId && x.TrainingDayId == session.TrainingDayId);
                if (!belongsToSessionDay) return Results.NotFound();
                result = new TrainingExerciseResult { TrainingSessionId = session.Id, TrainingExerciseId = trainingExerciseId };
                db.TrainingExerciseResults.Add(result);
            }
            result.IsCompleted = request.IsCompleted; result.DurationMinutes = request.DurationMinutes; result.Repetitions = request.Repetitions; result.Notes = request.Notes; result.PerceivedDifficulty = request.PerceivedDifficulty; result.CompletedAt = request.IsCompleted ? DateTimeOffset.UtcNow : null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        training.MapPost("/sessions/{sessionId:int}/complete", async (int sessionId, CompleteWorkoutRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var session = await db.TrainingSessions.Include(x => x.Results).ThenInclude(x => x.TrainingExercise).ThenInclude(x => x.Exercise).Include(x => x.TrainingDay).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).FirstOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == player.Id);
            if (session is null) return Results.NotFound();
            var hasIncompleteVisibleExercise = session.TrainingDay.Exercises.Where(x => IsVisibleExercise(x.Exercise)).Any(exercise => session.Results.FirstOrDefault(result => result.TrainingExerciseId == exercise.Id)?.IsCompleted != true);
            if (hasIncompleteVisibleExercise) return Results.ValidationProblem(new Dictionary<string, string[]> { ["exercises"] = ["Сначала отметьте все упражнения."] });
            session.Status = SessionStatus.Completed; session.CompletedAt = DateTimeOffset.UtcNow; session.Notes = request.Notes;
            if (!await db.PlayerAchievements.AnyAsync(x => x.PlayerId == player.Id && x.AchievementDefinition.Code == "FIRST_WORKOUT"))
            {
                var achievement = await db.AchievementDefinitions.FirstOrDefaultAsync(x => x.Code == "FIRST_WORKOUT");
                if (achievement is not null) db.PlayerAchievements.Add(new PlayerAchievement { PlayerId = player.Id, AchievementDefinitionId = achievement.Id });
            }
            db.AuditLogs.Add(new AuditLog { UserId = user.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "workout_completed", EntityType = nameof(TrainingSession), EntityId = session.Id.ToString() });
            await db.SaveChangesAsync();
            return Results.Ok(new { session.Id, session.CompletedAt });
        });

        playerApi.MapGet("/progress", async (ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            var player = await access.OwnPlayerAsync(user); if (player is null) return Results.NotFound();
            var sessions = await db.TrainingSessions.AsNoTracking().Where(x => x.PlayerId == player.Id).OrderByDescending(x => x.StartedAt).Take(30).Select(x => new { x.Id, x.Status, x.StartedAt, x.CompletedAt, x.TrainingDay.Title }).ToListAsync();
            var history = await db.SkillSnapshots.AsNoTracking().Where(x => x.PlayerId == player.Id).OrderBy(x => x.CapturedAt).Take(12).ToListAsync();
            var completed = sessions.Count(x => x.Status == SessionStatus.Completed);
            return Results.Ok(new { completedSessions = completed, adherence = sessions.Count == 0 ? 0 : (int)Math.Round(completed * 100m / sessions.Count), assessmentHistory = history.Select(x => new { x.CapturedAt, skills = SkillsDto(x) }), recentActivity = sessions });
        });
    }

    private static object PlayerDto(PlayerProfile player) => new { player.Id, player.FirstName, player.LastName, player.DateOfBirth, player.Gender, city = player.Municipality?.Name, player.PreferredPosition, player.DominantFoot, player.ExperienceLevel, player.Height, player.Weight };
    private static object SkillsDto(SkillSnapshot s) => new { speed = s.Speed, endurance = s.Endurance, ballControl = s.BallControl, passing = s.Passing, shooting = s.Shooting, agility = s.Agility };
    private static object PlanDto(TrainingPlan plan, IReadOnlyCollection<TrainingSession> sessions) => new { plan.Id, plan.WeekStart, status = plan.Status.ToString(), plan.GenerationReason, days = plan.Days.OrderBy(x => x.PlannedDate).Select(day => new { day.Id, day.PlannedDate, day.Title, session = sessions.Where(x => x.TrainingDayId == day.Id).Select(x => new { x.Id, status = x.Status.ToString(), x.CompletedAt }).FirstOrDefault(), exercises = day.Exercises.Where(x => IsVisibleExercise(x.Exercise)).OrderBy(x => x.SortOrder).Select(x => new { trainingExerciseId = x.Id, x.ExerciseId, x.Exercise.Name, x.Exercise.Description, x.Exercise.Instructions, skillCategory = x.Exercise.SkillCategory.ToString(), x.Exercise.Difficulty, durationMinutes = x.TargetDurationMinutes, x.TargetRepetitions, x.Exercise.Equipment }) }) };
    private static object SessionDto(TrainingSession session) => new { session.Id, status = session.Status.ToString(), session.StartedAt, session.CompletedAt, session.Notes, day = new { session.TrainingDay.Id, session.TrainingDay.Title, session.TrainingDay.PlannedDate }, exercises = session.TrainingDay.Exercises.Where(x => IsVisibleExercise(x.Exercise)).OrderBy(x => x.SortOrder).Select(x => { var result = session.Results.FirstOrDefault(r => r.TrainingExerciseId == x.Id); return new { trainingExerciseId = x.Id, x.Exercise.Name, x.Exercise.Instructions, skillCategory = x.Exercise.SkillCategory.ToString(), x.TargetDurationMinutes, x.TargetRepetitions, x.Exercise.Equipment, result = result is null ? null : new { result.IsCompleted, result.DurationMinutes, result.Repetitions, result.Notes, result.PerceivedDifficulty } }; }) };
    private static bool IsVisibleExercise(Exercise exercise) => exercise.IsActive && !exercise.Name.StartsWith("E2E ", StringComparison.OrdinalIgnoreCase) && !exercise.Name.StartsWith("Smoke CRUD ", StringComparison.OrdinalIgnoreCase);
}
