using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Roles.Admin).WithTags("Admin");
        admin.MapGet("/summary", async (int? days, AppDbContext db) =>
        {
            var periodDays = days is 7 or 30 or 90 ? days.Value : 30;
            var now = DateTimeOffset.UtcNow;
            var periodStart = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-(periodDays - 1)), TimeSpan.Zero);

            var users = await db.Users.CountAsync();
            var newUsers = await db.Users.CountAsync(x => x.CreatedAt >= periodStart);
            var activeUsers = await db.Users.CountAsync(x => x.LastActiveAt >= periodStart);
            var schools = await db.Schools.CountAsync();
            var activeSchools = await db.Schools.CountAsync(x => x.IsActive);
            var teams = await db.Teams.CountAsync();
            var activeTeams = await db.Teams.CountAsync(x => x.IsActive);
            var publishedActivities = await db.PublicActivities.CountAsync(x => x.Status == PublicActivityStatus.Published || x.Status == PublicActivityStatus.Full);
            var newActivities = await db.PublicActivities.CountAsync(x => x.CreatedAt >= periodStart);
            var upcomingActivities = await db.PublicActivities.CountAsync(x => x.StartAt >= now && (x.Status == PublicActivityStatus.Published || x.Status == PublicActivityStatus.Full));
            var registrations = await db.PublicActivityParticipants.CountAsync(x => x.Status != PublicParticipantStatus.Cancelled && x.Status != PublicParticipantStatus.Rejected);
            var newRegistrations = await db.PublicActivityParticipants.CountAsync(x => x.JoinedAt >= periodStart && x.Status != PublicParticipantStatus.Cancelled && x.Status != PublicParticipantStatus.Rejected);
            var completedTeamTrainings = await db.TeamTrainings.CountAsync(x => x.CompletedAt >= periodStart);
            var completedPersonalTrainings = await db.TrainingSessions.CountAsync(x => x.CompletedAt >= periodStart);

            var roleRows = await (from userRole in db.UserRoles.AsNoTracking()
                                  join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                                  select role.Name).ToListAsync();
            var roles = roleRows.Where(x => x is not null).GroupBy(x => x!).Select(x => new { role = x.Key, count = x.Count() }).OrderByDescending(x => x.count).ToArray();

            var userDates = await db.Users.AsNoTracking().Where(x => x.CreatedAt >= periodStart).Select(x => x.CreatedAt).ToListAsync();
            var activityDates = await db.PublicActivities.AsNoTracking().Where(x => x.PublishedAt >= periodStart).Select(x => x.PublishedAt!.Value).ToListAsync();
            var registrationDates = await db.PublicActivityParticipants.AsNoTracking().Where(x => x.JoinedAt >= periodStart && x.Status != PublicParticipantStatus.Cancelled && x.Status != PublicParticipantStatus.Rejected).Select(x => x.JoinedAt).ToListAsync();
            var teamTrainingDates = await db.TeamTrainings.AsNoTracking().Where(x => x.CompletedAt >= periodStart).Select(x => x.CompletedAt!.Value).ToListAsync();
            var personalTrainingDates = await db.TrainingSessions.AsNoTracking().Where(x => x.CompletedAt >= periodStart).Select(x => x.CompletedAt!.Value).ToListAsync();
            var trend = Enumerable.Range(0, periodDays).Select(offset =>
            {
                var date = periodStart.AddDays(offset).Date;
                return new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    users = userDates.Count(x => x.UtcDateTime.Date == date),
                    activities = activityDates.Count(x => x.UtcDateTime.Date == date),
                    registrations = registrationDates.Count(x => x.UtcDateTime.Date == date),
                    trainings = teamTrainingDates.Count(x => x.UtcDateTime.Date == date) + personalTrainingDates.Count(x => x.UtcDateTime.Date == date)
                };
            }).ToArray();

            var activityTypeRows = await db.PublicActivities.AsNoTracking()
                .Where(x => x.Status != PublicActivityStatus.Draft && x.Status != PublicActivityStatus.Archived)
                .Select(x => x.EventType).ToListAsync();
            var activityTypes = activityTypeRows.GroupBy(x => x).Select(x => new { type = x.Key.ToString(), count = x.Count() }).OrderByDescending(x => x.count).ToArray();
            var cityRows = await db.PublicActivities.AsNoTracking()
                .Where(x => x.Status != PublicActivityStatus.Draft && x.Status != PublicActivityStatus.Archived)
                .Select(x => x.Venue.City).ToListAsync();
            var topCities = cityRows.Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x).Select(x => new { city = x.Key, count = x.Count() }).OrderByDescending(x => x.count).ThenBy(x => x.city).Take(5).ToArray();

            return Results.Ok(new
            {
                periodDays,
                generatedAt = now,
                users,
                newUsers,
                activeUsers,
                players = await db.Players.CountAsync(),
                coaches = await db.CoachProfiles.CountAsync(),
                parents = await db.ParentProfiles.CountAsync(),
                organizers = await db.PublicOrganizerProfiles.CountAsync(),
                schools,
                activeSchools,
                teams,
                activeTeams,
                publishedActivities,
                newActivities,
                upcomingActivities,
                registrations,
                newRegistrations,
                completedTrainings = completedTeamTrainings + completedPersonalTrainings,
                exercises = await db.Exercises.CountAsync(),
                assessments = await db.AssessmentDefinitions.CountAsync(),
                programs = await db.TrainingPrograms.CountAsync(),
                auditEvents = await db.AuditLogs.CountAsync(),
                roles,
                trend,
                activityTypes,
                topCities
            });
        });

        admin.MapGet("/exercises", async (int page, int pageSize, AppDbContext db) =>
        {
            (page, pageSize) = Page(page, pageSize); var query = db.Exercises.AsNoTracking().OrderBy(x => x.Name);
            return Results.Ok(new { total = await query.CountAsync(), page, pageSize, items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.Name, x.Description, x.Instructions, skillCategory = x.SkillCategory.ToString(), x.Difficulty, x.DurationMinutes, x.Equipment, x.VideoUrl, x.ImageUrl, x.IsActive, x.UpdatedAt }).ToListAsync() });
        });
        admin.MapPost("/exercises", async (ExerciseUpsertRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Exercise(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
            var exercise = NewExercise(request); db.Exercises.Add(exercise); await db.SaveChangesAsync();
            await AddAudit(db, user, "admin_exercise_created", nameof(Exercise), exercise.Id.ToString()); return Results.Created($"/api/admin/exercises/{exercise.Id}", new { exercise.Id });
        });
        admin.MapPut("/exercises/{id:int}", async (int id, ExerciseUpsertRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Exercise(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
            var exercise = await db.Exercises.FindAsync(id); if (exercise is null) return Results.NotFound(); Apply(exercise, request); exercise.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
            await AddAudit(db, user, "admin_exercise_updated", nameof(Exercise), id.ToString()); return Results.NoContent();
        });
        admin.MapDelete("/exercises/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var exercise = await db.Exercises.FindAsync(id); if (exercise is null) return Results.NotFound();
            exercise.IsActive = false; exercise.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
            await AddAudit(db, user, "admin_exercise_deactivated", nameof(Exercise), id.ToString()); return Results.NoContent();
        });

        admin.MapGet("/assessments", async (AppDbContext db) => Results.Ok(await db.AssessmentDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Name, x.Description, x.Instructions, x.Unit, skillCategory = x.SkillCategory.ToString(), scoringDirection = x.ScoringDirection.ToString(), x.MinimumReasonableValue, x.MaximumReasonableValue, x.SortOrder, x.IsActive, norms = db.AssessmentNorms.Where(n => n.AssessmentDefinitionId == x.Id).OrderBy(n => n.MinimumAge).Select(n => new { n.Id, n.MinimumAge, n.MaximumAge, n.LowPerformanceValue, n.HighPerformanceValue, n.IsDemo, n.SourceNote }) }).ToListAsync()));
        admin.MapPost("/assessments", async (AssessmentUpsertRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Assessment(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
            var item = new AssessmentDefinition { Name = "", Description = "", Instructions = "", Unit = "" }; Apply(item, request); db.AssessmentDefinitions.Add(item); db.AssessmentNorms.AddRange(NewNorms(item, request.Norms)); await db.SaveChangesAsync();
            await AddAudit(db, user, "admin_assessment_created", nameof(AssessmentDefinition), item.Id.ToString()); return Results.Created($"/api/admin/assessments/{item.Id}", new { item.Id });
        });
        admin.MapPut("/assessments/{id:int}", async (int id, AssessmentUpsertRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Assessment(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
            var item = await db.AssessmentDefinitions.FindAsync(id); if (item is null) return Results.NotFound();
            Apply(item, request); var oldNorms = await db.AssessmentNorms.Where(x => x.AssessmentDefinitionId == id).ToListAsync(); db.AssessmentNorms.RemoveRange(oldNorms); db.AssessmentNorms.AddRange(NewNorms(item, request.Norms)); await db.SaveChangesAsync();
            await AddAudit(db, user, "admin_assessment_updated", nameof(AssessmentDefinition), id.ToString()); return Results.NoContent();
        });
        admin.MapDelete("/assessments/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var item = await db.AssessmentDefinitions.FindAsync(id); if (item is null) return Results.NotFound(); item.IsActive = false; await db.SaveChangesAsync(); await AddAudit(db, user, "admin_assessment_deactivated", nameof(AssessmentDefinition), id.ToString()); return Results.NoContent();
        });

        admin.MapGet("/programs", async (AppDbContext db) => Results.Ok(await db.TrainingPrograms.AsNoTracking().OrderBy(x => x.Name).ToListAsync()));
        admin.MapPost("/programs", async (TrainingProgramUpsertRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Program(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
            var item = new TrainingProgram { Name = request.Name.Trim(), Description = request.Description.Trim(), Weeks = request.Weeks, IsActive = request.IsActive }; db.TrainingPrograms.Add(item); await db.SaveChangesAsync(); await AddAudit(db, user, "admin_program_created", nameof(TrainingProgram), item.Id.ToString()); return Results.Created($"/api/admin/programs/{item.Id}", new { item.Id });
        });
        admin.MapPut("/programs/{id:int}", async (int id, TrainingProgramUpsertRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Program(request); if (errors.Count > 0) return Results.ValidationProblem(errors); var item = await db.TrainingPrograms.FindAsync(id); if (item is null) return Results.NotFound(); item.Name = request.Name.Trim(); item.Description = request.Description.Trim(); item.Weeks = request.Weeks; item.IsActive = request.IsActive; await db.SaveChangesAsync(); await AddAudit(db, user, "admin_program_updated", nameof(TrainingProgram), id.ToString()); return Results.NoContent();
        });
        admin.MapDelete("/programs/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) => { var item = await db.TrainingPrograms.FindAsync(id); if (item is null) return Results.NotFound(); item.IsActive = false; await db.SaveChangesAsync(); await AddAudit(db, user, "admin_program_deactivated", nameof(TrainingProgram), id.ToString()); return Results.NoContent(); });

        admin.MapGet("/municipalities", async (AppDbContext db) => Results.Ok(await db.Municipalities.AsNoTracking().OrderBy(x => x.Name).ToListAsync()));
        admin.MapPost("/municipalities", async (MunicipalityRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Municipality(request); if (errors.Count > 0) return Results.ValidationProblem(errors); if (await db.Municipalities.AnyAsync(x => x.Name == request.Name.Trim())) return Results.Conflict(new { message = "Такой город уже есть в справочнике." });
            var item = new Municipality { Name = request.Name.Trim(), Region = request.Region.Trim(), IsActive = request.IsActive }; db.Municipalities.Add(item); await db.SaveChangesAsync();
            await AddAudit(db, user, "admin_municipality_created", nameof(Municipality), item.Id.ToString()); return Results.Created($"/api/admin/municipalities/{item.Id}", new { item.Id });
        });
        admin.MapPut("/municipalities/{id:int}", async (int id, MunicipalityRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var errors = Validation.Municipality(request); if (errors.Count > 0) return Results.ValidationProblem(errors); var item = await db.Municipalities.FindAsync(id); if (item is null) return Results.NotFound();
            if (await db.Municipalities.AnyAsync(x => x.Id != id && x.Name == request.Name.Trim())) return Results.Conflict(new { message = "Такой город уже есть в справочнике." });
            item.Name = request.Name.Trim(); item.Region = request.Region.Trim(); item.IsActive = request.IsActive; await db.SaveChangesAsync(); await AddAudit(db, user, "admin_municipality_updated", nameof(Municipality), id.ToString()); return Results.NoContent();
        });
        admin.MapDelete("/municipalities/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) => { var item = await db.Municipalities.FindAsync(id); if (item is null) return Results.NotFound(); item.IsActive = false; await db.SaveChangesAsync(); await AddAudit(db, user, "admin_municipality_deactivated", nameof(Municipality), id.ToString()); return Results.NoContent(); });

        admin.MapPost("/users", async (InviteUserRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db, IConfiguration configuration, IOptions<DataProtectionTokenProviderOptions> tokenOptions) =>
        {
            var email = request.Email?.Trim();
            var allowedRoles = new[] { Roles.Coach, Roles.Parent, Roles.Admin };
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Укажите корректный email."] });
            if (!allowedRoles.Contains(request.Role))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Через приглашение можно создать тренера, родителя или администратора. Игрок и организатор регистрируются самостоятельно."] });
            if (await users.FindByEmailAsync(email) is not null) return Results.Conflict(new { message = "Пользователь с таким email уже существует." });

            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            var user = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = true, LockoutEnabled = true };
            var createResult = await users.CreateAsync(user);
            if (!createResult.Succeeded)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = createResult.Errors.Select(x => x.Description).ToArray() });
            var roleResult = await users.AddToRoleAsync(user, request.Role);
            if (!roleResult.Succeeded)
            {
                if (transaction is null) await users.DeleteAsync(user);
                return Results.Problem("Не удалось назначить роль приглашённому пользователю.", statusCode: 500);
            }

            if (request.Role == Roles.Coach)
                db.CoachProfiles.Add(new CoachProfile { UserId = user.Id, DisplayName = email.Split('@')[0] });
            else if (request.Role == Roles.Parent)
                db.ParentProfiles.Add(new ParentProfile { UserId = user.Id });

            await AddAudit(db, principal, "user_invited", nameof(ApplicationUser), user.Id, $"role:{request.Role}");
            if (transaction is not null) await transaction.CommitAsync();

            var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(user));
            var inviteUrl = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");
            return Results.Created($"/api/admin/users/{user.Id}", new
            {
                user.Id,
                user.Email,
                role = request.Role,
                inviteUrl,
                expiresAt = DateTimeOffset.UtcNow.Add(tokenOptions.Value.TokenLifespan)
            });
        });

        admin.MapPost("/users/{id}/invite", async (string id, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db, IConfiguration configuration, IOptions<DataProtectionTokenProviderOptions> tokenOptions) =>
        {
            var target = await users.FindByIdAsync(id); if (target is null) return Results.NotFound();
            var hasPassword = await users.HasPasswordAsync(target);
            if (await users.IsLockedOutAsync(target))
                return Results.Conflict(new { message = "Учётная запись заблокирована. Сначала разблокируйте." });

            await AddAudit(db, principal, hasPassword ? "user_password_reset_by_admin" : "user_invite_reissued", nameof(ApplicationUser), target.Id);
            var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(target));
            var inviteUrl = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(target.Email!)}&token={Uri.EscapeDataString(token)}");
            return Results.Ok(new { target.Id, target.Email, inviteUrl, expiresAt = DateTimeOffset.UtcNow.Add(tokenOptions.Value.TokenLifespan) });
        });

        admin.MapGet("/users", async (int page, int pageSize, AppDbContext db) =>
        {
            (page, pageSize) = Page(page, pageSize); var query = db.Users.AsNoTracking().OrderBy(x => x.Email);
            var now = DateTimeOffset.UtcNow;
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new
            {
                x.Id,
                x.Email,
                x.LockoutEnd,
                isLocked = x.LockoutEnd != null && x.LockoutEnd > now,
                hasPassword = x.PasswordHash != null,
                x.CreatedAt,
                roles = (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == x.Id select role.Name).ToList()
            }).ToListAsync();
            return Results.Ok(new { total = await query.CountAsync(), page, pageSize, items });
        });
        admin.MapPut("/users/{id}/lock", async (string id, UserLockRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            if (principal.FindFirstValue(ClaimTypes.NameIdentifier) == id)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["user"] = ["Нельзя заблокировать собственную учётную запись."] });
            var target = await users.FindByIdAsync(id); if (target is null) return Results.NotFound();
            var enabledResult = await users.SetLockoutEnabledAsync(target, true);
            var lockResult = await users.SetLockoutEndDateAsync(target, request.Locked ? DateTimeOffset.UtcNow.AddYears(100) : null);
            if (!enabledResult.Succeeded || !lockResult.Succeeded) return Results.Problem("Не удалось изменить блокировку пользователя.", statusCode: 500);
            if (!request.Locked) await users.ResetAccessFailedCountAsync(target);
            await users.UpdateSecurityStampAsync(target);
            await AddAudit(db, principal, request.Locked ? "user_locked" : "user_unlocked", nameof(ApplicationUser), id);
            return Results.NoContent();
        });
        admin.MapPut("/users/{id}/roles", async (string id, string[] roleNames, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var assignableRoles = Roles.All.ToHashSet();
            if (roleNames.Any(x => !assignableRoles.Contains(x))) return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["Неизвестная или отключённая роль."] });
            var target = await users.FindByIdAsync(id); if (target is null) return Results.NotFound();
            if (roleNames.Contains(Roles.Organizer) && !await db.PublicOrganizerProfiles.AnyAsync(x => x.UserId == id))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["Роль организатора создаётся через взрослую регистрацию с подтверждением возраста."] });
            var current = await users.GetRolesAsync(target); await users.RemoveFromRolesAsync(target, current); await users.AddToRolesAsync(target, roleNames.Distinct());
            await users.UpdateSecurityStampAsync(target);
            await AddAudit(db, principal, "role_changed", nameof(ApplicationUser), id, string.Join(',', roleNames)); return Results.NoContent();
        });
        admin.MapGet("/coach-links", async (AppDbContext db) => Results.Ok(await db.CoachPlayerLinks.AsNoTracking().Select(x => new { x.CoachId, coach = x.Coach.DisplayName, x.PlayerId, player = x.Player.FirstName + " " + x.Player.LastName, status = x.Status.ToString(), x.CreatedAt }).ToListAsync()));
        admin.MapPost("/coach-links", async (int coachId, int playerId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.CoachPlayerLinks.AnyAsync(x => x.CoachId == coachId && x.PlayerId == playerId)) return Results.Conflict();
            db.CoachPlayerLinks.Add(new CoachPlayerLink { CoachId = coachId, PlayerId = playerId }); await db.SaveChangesAsync(); await AddAudit(db, user, "coach_player_link_created", nameof(CoachPlayerLink), $"{coachId}:{playerId}"); return Results.NoContent();
        });
    }

    private static (int page, int pageSize) Page(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100));
    private static Exercise NewExercise(ExerciseUpsertRequest x) { var entity = new Exercise { Name = "", Description = "", Instructions = "", Equipment = "" }; Apply(entity, x); return entity; }
    private static void Apply(Exercise e, ExerciseUpsertRequest x) { e.Name = x.Name.Trim(); e.Description = x.Description.Trim(); e.Instructions = x.Instructions.Trim(); e.SkillCategory = x.SkillCategory; e.Difficulty = x.Difficulty; e.DurationMinutes = x.DurationMinutes; e.Equipment = x.Equipment.Trim(); e.VideoUrl = x.VideoUrl; e.ImageUrl = x.ImageUrl; e.IsActive = x.IsActive; }
    private static void Apply(AssessmentDefinition item, AssessmentUpsertRequest request) { item.Name = request.Name.Trim(); item.Description = request.Description.Trim(); item.Instructions = request.Instructions.Trim(); item.Unit = request.Unit.Trim(); item.SkillCategory = request.SkillCategory; item.ScoringDirection = request.ScoringDirection; item.MinimumReasonableValue = request.MinimumReasonableValue; item.MaximumReasonableValue = request.MaximumReasonableValue; item.SortOrder = request.SortOrder; item.IsActive = request.IsActive; }
    private static IEnumerable<AssessmentNorm> NewNorms(AssessmentDefinition item, IEnumerable<AssessmentNormRequest> norms) => norms.Select(x => new AssessmentNorm { AssessmentDefinition = item, MinimumAge = x.MinimumAge, MaximumAge = x.MaximumAge, LowPerformanceValue = x.LowPerformanceValue, HighPerformanceValue = x.HighPerformanceValue, IsDemo = x.IsDemo, SourceNote = x.SourceNote.Trim() });
    private static async Task AddAudit(AppDbContext db, ClaimsPrincipal user, string type, string entity, string? entityId, string? details = null) { db.AuditLogs.Add(new AuditLog { UserId = user.FindFirstValue(ClaimTypes.NameIdentifier), EventType = type, EntityType = entity, EntityId = entityId, Details = details }); await db.SaveChangesAsync(); }
}
