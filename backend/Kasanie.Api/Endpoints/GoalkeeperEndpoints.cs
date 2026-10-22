using System.Security.Claims;
using System.Text.Json;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static readonly JsonSerializerOptions GoalkeeperJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] KeeperAttendances = ["Unknown", "Present", "Late", "Absent", "Excused"];
    private static readonly string[] KeeperStatuses = ["Planned", "InProgress", "Completed", "Cancelled"];
    private static readonly string[] KeeperIntensities = ["Низкая", "Средняя", "Высокая"];
    private static readonly string[] KeeperPositions = ["в", "вр", "gk", "goalkeeper"];

    private static IQueryable<GoalkeeperCoachAssignment> KeeperAppointments(AppDbContext db, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return db.GoalkeeperCoachAssignments.Where(x => x.Coach.UserId == userId && x.Team.IsActive && x.Team.School.IsActive &&
            db.SchoolMemberships.Any(m => m.SchoolId == x.Team.SchoolId && m.UserId == userId && m.IsActive));
    }

    private static IQueryable<TeamPlayer> KeeperRoster(AppDbContext db, int teamId) => db.TeamPlayers.Where(x => x.TeamId == teamId && x.IsActive &&
        (x.Player.PreferredPosition.ToLower().Contains("вратар") || KeeperPositions.Contains(x.Player.PreferredPosition.ToLower())));

    private static void MapGoalkeeperTraining(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api/school/{schoolId:int}/teams/{teamId:int}/goalkeeper-coaches").RequireAuthorization();
        staff.MapGet("/", async (int schoolId, int teamId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!await CanAppointKeeperCoach(db, user, schoolId)) return Results.Forbid();
            if (!await db.Teams.AnyAsync(x => x.Id == teamId && x.SchoolId == schoolId)) return Results.NotFound();
            return Results.Ok(await db.GoalkeeperCoachAssignments.AsNoTracking().Where(x => x.TeamId == teamId)
                .OrderBy(x => x.Coach.DisplayName).Select(x => new { x.CoachId, x.Coach.DisplayName }).ToListAsync());
        });
        staff.MapPost("/", async (int schoolId, int teamId, GoalkeeperAppointmentRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!await CanAppointKeeperCoach(db, user, schoolId)) return Results.Forbid();
            if (!await db.Teams.AnyAsync(x => x.Id == teamId && x.SchoolId == schoolId && x.IsActive && x.School.IsActive)) return Results.NotFound();
            if (!await CoachBelongsToSchoolAsync(db, schoolId, request.CoachId)) return Results.BadRequest(new { message = "Выберите активного тренера этой школы." });
            if (await db.GoalkeeperCoachAssignments.AnyAsync(x => x.TeamId == teamId && x.CoachId == request.CoachId)) return Results.NoContent();
            db.GoalkeeperCoachAssignments.Add(new GoalkeeperCoachAssignment { TeamId = teamId, CoachId = request.CoachId });
            var coachUserId = await db.CoachProfiles.Where(x => x.Id == request.CoachId).Select(x => x.UserId).SingleAsync();
            var membership = await db.SchoolMemberships.SingleAsync(x => x.SchoolId == schoolId && x.UserId == coachUserId);
            if (membership.Role == SchoolMembershipRole.Coach && !await db.TeamCoaches.AnyAsync(x => x.CoachId == request.CoachId && x.Team.SchoolId == schoolId && x.Team.IsActive))
                membership.Role = SchoolMembershipRole.GoalkeeperCoach;
            db.AuditLogs.Add(new AuditLog { UserId = user.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "goalkeeper_coach_assigned", EntityType = nameof(GoalkeeperCoachAssignment), EntityId = $"{teamId}:{request.CoachId}" });
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { message = "Назначение изменилось параллельно. Обновите список." }); }
            return Results.NoContent();
        });
        staff.MapDelete("/{coachId:int}", async (int schoolId, int teamId, int coachId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!await CanAppointKeeperCoach(db, user, schoolId)) return Results.Forbid();
            var link = await db.GoalkeeperCoachAssignments.SingleOrDefaultAsync(x => x.TeamId == teamId && x.CoachId == coachId && x.Team.SchoolId == schoolId);
            if (link is null) return Results.NotFound();
            db.GoalkeeperCoachAssignments.Remove(link);
            await SaveCoachAudit(db, user, "goalkeeper_coach_removed", nameof(GoalkeeperCoachAssignment), $"{teamId}:{coachId}");
            return Results.NoContent();
        });

        var keepers = app.MapGroup("/api/coach/goalkeepers").RequireAuthorization(Roles.Coach).WithTags("Goalkeeper coach pilot");
        keepers.MapGet("/teams", async (ClaimsPrincipal user, AppDbContext db) => Results.Ok(await KeeperAppointments(db, user).AsNoTracking()
            .OrderBy(x => x.Team.School.Name).ThenBy(x => x.Team.Name).Select(x => new
            {
                x.TeamId, x.Team.SchoolId, school = x.Team.School.Name,
                name = (x.Team.AgeGroup ?? "") + (x.Team.AgeGroup == null ? "" : " — ") + x.Team.Name,
                x.CoachId, coach = x.Coach.DisplayName
            }).ToListAsync()));

        keepers.MapGet("/teams/{teamId:int}/workspace", async (int teamId, ClaimsPrincipal user, AppDbContext db) =>
        {
            var appointment = await KeeperAppointments(db, user).AsNoTracking().SingleOrDefaultAsync(x => x.TeamId == teamId);
            if (appointment is null) return Results.Forbid();
            var players = await KeeperRoster(db, teamId).AsNoTracking().OrderBy(x => x.Player.LastName).Select(x => new
            {
                x.PlayerId, x.Player.FirstName, x.Player.LastName, x.Player.DateOfBirth, x.Player.PreferredPosition,
                x.Player.DominantFoot, x.Player.Height, x.Player.Weight, x.ShirtNumber
            }).ToListAsync();
            var records = await db.GoalkeeperPlayerRecords.AsNoTracking().Where(x => x.TeamId == teamId && x.CoachId == appointment.CoachId).ToListAsync();
            var trainings = await db.GoalkeeperTrainings.AsNoTracking().Where(x => x.TeamId == teamId && x.CoachId == appointment.CoachId).OrderByDescending(x => x.ScheduledAt).ToListAsync();
            var history = trainings.Where(x => x.Status == "Completed").Select(x => ReadKeeperState(x)).ToList();
            return Results.Ok(new
            {
                players = players.Select(p => new
                {
                    p.PlayerId, p.FirstName, p.LastName, p.DateOfBirth, p.PreferredPosition, p.DominantFoot, p.Height, p.Weight, p.ShirtNumber,
                    record = records.Where(r => r.PlayerId == p.PlayerId).Select(r => new { r.Goal, r.Notes, r.Version }).FirstOrDefault(),
                    sessions = history.Count(t => t.Players.Any(a => a.PlayerId == p.PlayerId)),
                    attended = history.Count(t => t.Players.Any(a => a.PlayerId == p.PlayerId && a.Attendance is "Present" or "Late"))
                }),
                exercises = await db.GoalkeeperExercises.AsNoTracking().Where(x => x.TeamId == teamId && x.CoachId == appointment.CoachId).OrderBy(x => x.Title)
                    .Select(x => new { x.Id, x.Title, x.Category, x.Description, x.Equipment, x.DurationMinutes, x.IsActive }).ToListAsync(),
                trainings = trainings.Select(t => new { t.Id, t.Title, t.ScheduledAt, t.DurationMinutes, t.Status, t.Version, t.CompletedAt, players = ReadKeeperState(t).Players.Count }),
                matches = await db.TeamMatches.AsNoTracking().Where(x => x.TeamId == teamId && x.ScheduledAt > DateTimeOffset.UtcNow.AddYears(-1))
                    .OrderBy(x => x.ScheduledAt).Select(x => new { x.Id, x.Opponent, x.ScheduledAt, x.Venue }).ToListAsync()
            });
        });

        keepers.MapPost("/teams/{teamId:int}/players", async (int teamId, GoalkeeperCreatePlayerRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!await KeeperAppointments(db, user).AnyAsync(x => x.TeamId == teamId)) return Results.Forbid();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length > 100 || string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length > 100 ||
                request.DateOfBirth > today.AddYears(-4) || request.DateOfBirth < today.AddYears(-90) || request.DominantFoot is not ("Правая" or "Левая" or "Обе") || request.ShirtNumber is < 1 or > 99)
                return Results.BadRequest(new { message = "Проверьте имя, фамилию, дату рождения, ногу и номер (1–99)." });
            if (request.ShirtNumber.HasValue && await db.TeamPlayers.AnyAsync(x => x.TeamId == teamId && x.IsActive && x.ShirtNumber == request.ShirtNumber))
                return Results.Conflict(new { message = "Этот номер уже занят в составе." });
            var player = new PlayerProfile { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DateOfBirth = request.DateOfBirth, PreferredPosition = "Вратарь", DominantFoot = request.DominantFoot, ExperienceLevel = "Любитель" };
            db.Players.Add(player);
            db.TeamPlayers.Add(new TeamPlayer { TeamId = teamId, Player = player, ShirtNumber = request.ShirtNumber });
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { message = "Состав изменился параллельно. Проверьте номер игрока." }); }
            await SaveCoachAudit(db, user, "goalkeeper_player_created", nameof(PlayerProfile), player.Id.ToString());
            return Results.Created($"/api/coach/goalkeepers/teams/{teamId}/players/{player.Id}", new { player.Id });
        });

        keepers.MapPut("/teams/{teamId:int}/players/{playerId:int}", async (int teamId, int playerId, GoalkeeperRecordRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var appointment = await KeeperAppointments(db, user).SingleOrDefaultAsync(x => x.TeamId == teamId);
            if (appointment is null) return Results.Forbid();
            if (!await KeeperRoster(db, teamId).AnyAsync(x => x.PlayerId == playerId)) return Results.NotFound();
            if ((request.Goal?.Length ?? 0) > 2000 || (request.Notes?.Length ?? 0) > 4000) return Results.BadRequest(new { message = "Слишком длинная цель или заметка." });
            var record = await db.GoalkeeperPlayerRecords.FindAsync(teamId, appointment.CoachId, playerId);
            if ((record?.Version ?? 0) != request.Version) return Results.Conflict(new { message = "Карточка изменена в другой вкладке. Обновите страницу." });
            if (record is null) { record = new GoalkeeperPlayerRecord { TeamId = teamId, CoachId = appointment.CoachId, PlayerId = playerId }; db.GoalkeeperPlayerRecords.Add(record); }
            else record.Version++;
            record.Goal = request.Goal?.Trim() ?? ""; record.Notes = request.Notes?.Trim() ?? ""; record.UpdatedAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = user.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "goalkeeper_record_updated", EntityType = nameof(GoalkeeperPlayerRecord), EntityId = $"{teamId}:{playerId}" });
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { message = "Карточка изменена параллельно. Обновите страницу." }); }
            return Results.Ok(new { record.Version });
        });

        keepers.MapPost("/teams/{teamId:int}/exercises", async (int teamId, GoalkeeperExerciseRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var appointment = await KeeperAppointments(db, user).SingleOrDefaultAsync(x => x.TeamId == teamId);
            if (appointment is null) return Results.Forbid();
            if (!ValidKeeperExercise(request)) return Results.BadRequest(new { message = "Укажите название, категорию и длительность упражнения (1–180 мин). Описание — до 4000 символов." });
            var exercise = new GoalkeeperExercise { TeamId = teamId, CoachId = appointment.CoachId, Title = request.Title.Trim(), Category = request.Category.Trim(), Description = request.Description ?? "", Equipment = request.Equipment ?? "", DurationMinutes = request.DurationMinutes, IsActive = request.IsActive };
            db.GoalkeeperExercises.Add(exercise); await db.SaveChangesAsync();
            await SaveCoachAudit(db, user, "goalkeeper_exercise_created", nameof(GoalkeeperExercise), exercise.Id.ToString());
            return Results.Created($"/api/coach/goalkeepers/exercises/{exercise.Id}", new { exercise.Id });
        });
        keepers.MapPut("/exercises/{id:int}", async (int id, GoalkeeperExerciseRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var exercise = await db.GoalkeeperExercises.SingleOrDefaultAsync(x => x.Id == id);
            if (exercise is null) return Results.NotFound();
            if (!await KeeperAppointments(db, user).AnyAsync(x => x.TeamId == exercise.TeamId && x.CoachId == exercise.CoachId)) return Results.Forbid();
            if (!ValidKeeperExercise(request)) return Results.BadRequest(new { message = "Проверьте название, категорию, длительность и описание." });
            exercise.Title = request.Title.Trim(); exercise.Category = request.Category.Trim(); exercise.Description = request.Description ?? ""; exercise.Equipment = request.Equipment ?? ""; exercise.DurationMinutes = request.DurationMinutes; exercise.IsActive = request.IsActive; exercise.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveCoachAudit(db, user, "goalkeeper_exercise_updated", nameof(GoalkeeperExercise), exercise.Id.ToString());
            return Results.NoContent();
        });

        keepers.MapGet("/trainings/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var training = await db.GoalkeeperTrainings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (training is null) return Results.NotFound();
            if (!await KeeperAppointments(db, user).AnyAsync(x => x.TeamId == training.TeamId && x.CoachId == training.CoachId)) return Results.Forbid();
            var state = ReadKeeperState(training);
            var ids = state.Players.Select(x => x.PlayerId).ToArray();
            return Results.Ok(new { training.Id, training.TeamId, training.Version, training.Status, training.CompletedAt, state,
                players = await db.Players.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { playerId = x.Id, x.FirstName, x.LastName }).ToListAsync() });
        });
        keepers.MapPost("/teams/{teamId:int}/trainings", async (int teamId, GoalkeeperTrainingRequest request, ClaimsPrincipal user, AppDbContext db) =>
            await SaveKeeperTraining(db, user, teamId, null, request));
        keepers.MapPut("/teams/{teamId:int}/trainings/{id:int}", async (int teamId, int id, GoalkeeperTrainingRequest request, ClaimsPrincipal user, AppDbContext db) =>
            await SaveKeeperTraining(db, user, teamId, id, request));
    }

    private static bool ValidKeeperExercise(GoalkeeperExerciseRequest request) => !string.IsNullOrWhiteSpace(request.Title) && request.Title.Length <= 180 &&
        !string.IsNullOrWhiteSpace(request.Category) && request.Category.Length <= 80 && (request.Description?.Length ?? 0) <= 4000 &&
        (request.Equipment?.Length ?? 0) <= 500 && request.DurationMinutes is >= 1 and <= 180;

    private static Task<bool> CanAppointKeeperCoach(AppDbContext db, ClaimsPrincipal user, int schoolId)
    {
        if (user.IsInRole(Roles.Admin)) return db.Schools.AnyAsync(x => x.Id == schoolId);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return db.SchoolMemberships.AnyAsync(x => x.SchoolId == schoolId && x.UserId == userId && x.IsActive && x.School.IsActive &&
            (x.Role == SchoolMembershipRole.Owner || x.Role == SchoolMembershipRole.Administrator));
    }

    private static GoalkeeperTrainingRequest ReadKeeperState(GoalkeeperTraining training) =>
        JsonSerializer.Deserialize<GoalkeeperTrainingRequest>(training.StateJson, GoalkeeperJson)!;

    private static async Task<IResult> SaveKeeperTraining(AppDbContext db, ClaimsPrincipal user, int teamId, int? id, GoalkeeperTrainingRequest request)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var appointment = await KeeperAppointments(db, user).SingleOrDefaultAsync(x => x.TeamId == teamId);
        if (appointment is null) return Results.Forbid();
        GoalkeeperTraining? training = null;
        if (id.HasValue)
        {
            training = await db.GoalkeeperTrainings.SingleOrDefaultAsync(x => x.Id == id && x.TeamId == teamId && x.CoachId == appointment.CoachId);
            if (training is null) return Results.NotFound();
            if (training.Version != request.Version) return Results.Conflict(new { message = "Тренировка изменена в другой вкладке. Обновите страницу." });
            if (training.Status is "Completed" or "Cancelled") return Results.Conflict(new { message = "Завершённая или отменённая тренировка сохранена в истории и недоступна для редактирования." });
            if (training.Status == "InProgress" && request.Status == "Planned") return Results.Conflict(new { message = "Начатое занятие нельзя вернуть в план. Сохраните итог или отмените занятие." });
        }
        else if (request.Version != 0) return Results.BadRequest(new { message = "У новой тренировки версия должна быть 0." });

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 180 || request.Plan is null || request.Plan.Count is < 1 or > 30 || request.Plan.Any(x => x is null) ||
            request.Players is null || request.Players.Count is < 1 or > 100 || request.Players.Any(x => x is null) || !KeeperStatuses.Contains(request.Status) || !KeeperIntensities.Contains(request.Intensity) ||
            (request.Venue?.Length ?? 0) > 500 || (request.Objective?.Length ?? 0) > 2000 || (request.Notes?.Length ?? 0) > 4000 ||
            request.ScheduledAt.Year is < 2020 or > 2100)
            return Results.BadRequest(new { message = "Заполните тему, дату, интенсивность, 1–30 этапов и список вратарей." });
        if (request.Plan.Any(x => string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 180 || x.DurationMinutes is < 1 or > 180 ||
            x.Stage is not ("Разминка" or "Основная часть" or "Игровая часть" or "Заминка") || (x.Instructions?.Length ?? 0) > 4000) || request.Plan.Sum(x => x.DurationMinutes) > 360)
            return Results.BadRequest(new { message = "Проверьте этапы: длительность каждого 1–180 мин, всего до 360 мин." });
        if (request.Players.Select(x => x.PlayerId).Distinct().Count() != request.Players.Count || request.Players.Any(x => !KeeperAttendances.Contains(x.Attendance) || x.Rating is < 1 or > 10 || (x.Notes?.Length ?? 0) > 2000))
            return Results.BadRequest(new { message = "Проверьте вратарей, посещаемость и оценки (1–10). Повторов быть не должно." });
        if (request.Status == "Completed" && (request.Players.Any(x => x.Attendance == "Unknown") || request.ScheduledAt > DateTimeOffset.UtcNow.AddMinutes(10)))
            return Results.BadRequest(new { message = "Завершите посещаемость каждого вратаря. Будущее занятие завершить нельзя." });
        if (training is null && request.Status == "Cancelled") return Results.BadRequest(new { message = "Отменять можно уже созданное занятие." });
        var oldState = training is null ? null : ReadKeeperState(training);
        if (training?.Status == "InProgress" && !(oldState!.Players.Select(x => x.PlayerId).ToHashSet().SetEquals(request.Players.Select(x => x.PlayerId))))
            return Results.Conflict(new { message = "После начала занятия состав участников менять нельзя." });
        var validPlayers = await KeeperRoster(db, teamId).Select(x => x.PlayerId).ToListAsync();
        // Departed players remain in an existing journal, but cannot be added to a new session.
        var historicIds = oldState?.Players.Select(x => x.PlayerId).ToHashSet() ?? [];
        if (request.Players.Any(x => !validPlayers.Contains(x.PlayerId) && !historicIds.Contains(x.PlayerId)))
            return Results.BadRequest(new { message = "Для нового участника выберите действующего вратаря этого состава." });
        var exerciseIds = request.Plan.Where(x => x.ExerciseId.HasValue).Select(x => x.ExerciseId!.Value).Distinct().ToArray();
        var validExercises = await db.GoalkeeperExercises.Where(x => exerciseIds.Contains(x.Id) && x.TeamId == teamId && x.CoachId == appointment.CoachId && x.IsActive).Select(x => x.Id).ToListAsync();
        var historicExercises = oldState?.Plan.Where(x => x.ExerciseId.HasValue).Select(x => x.ExerciseId!.Value).ToHashSet() ?? [];
        if (exerciseIds.Any(x => !validExercises.Contains(x) && !historicExercises.Contains(x)))
            return Results.BadRequest(new { message = "Одно из упражнений недоступно в вашем каталоге." });
        var duration = request.Plan.Sum(x => x.DurationMinutes);
        var startsAt = request.ScheduledAt.ToUniversalTime();
        var endsAt = startsAt.AddMinutes(duration);
        if (request.Status != "Cancelled")
        {
            var possible = await db.GoalkeeperTrainings.AsNoTracking().Where(x => x.CoachId == appointment.CoachId && x.Id != (id ?? 0) && x.Status != "Cancelled" && x.ScheduledAt < endsAt && x.ScheduledAt > startsAt.AddHours(-6)).ToListAsync();
            if (possible.Any(x => x.ScheduledAt.AddMinutes(x.DurationMinutes) > startsAt)) return Results.Conflict(new { message = "У вас уже есть вратарская тренировка в это время." });
        }
        training ??= new GoalkeeperTraining { TeamId = teamId, CoachId = appointment.CoachId, Title = request.Title.Trim() };
        if (!id.HasValue) db.GoalkeeperTrainings.Add(training); else training.Version++;
        training.Title = request.Title.Trim(); training.ScheduledAt = startsAt; training.DurationMinutes = duration;
        training.Status = request.Status; training.UpdatedAt = DateTimeOffset.UtcNow;
        if (request.Status == "Completed") training.CompletedAt = DateTimeOffset.UtcNow;
        // Plans are snapshots: changing a library exercise must never rewrite an old session.
        training.StateJson = JsonSerializer.Serialize(request with { Title = training.Title, ScheduledAt = startsAt, Version = training.Version }, GoalkeeperJson);
        db.AuditLogs.Add(new AuditLog { UserId = user.FindFirstValue(ClaimTypes.NameIdentifier), EventType = $"goalkeeper_training_{(id.HasValue ? "updated" : "created")}", EntityType = nameof(GoalkeeperTraining), EntityId = id?.ToString(), Details = $"team={teamId};status={request.Status}" });
        try
        {
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "Тренировка изменена параллельно. Обновите страницу." }); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "40001" or "40P01" or "23505" }) { return Results.Conflict(new { message = "Расписание изменилось параллельно. Обновите страницу." }); }
        catch (Npgsql.PostgresException e) when (e.SqlState is "40001" or "40P01") { return Results.Conflict(new { message = "Расписание изменилось параллельно. Обновите страницу." }); }
        return Results.Ok(new { training.Id, training.Version, training.Status });
    }
}
