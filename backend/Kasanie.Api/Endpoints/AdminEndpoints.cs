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
        admin.MapGet("/summary", async (AppDbContext db) => Results.Ok(new
        {
            users = await db.Users.CountAsync(), players = await db.Players.CountAsync(), exercises = await db.Exercises.CountAsync(),
            assessments = await db.AssessmentDefinitions.CountAsync(), programs = await db.TrainingPrograms.CountAsync(), auditEvents = await db.AuditLogs.CountAsync()
        }));

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
            var allowedRoles = new[] { Roles.Coach, Roles.Parent, Roles.RegionalAnalyst, Roles.Admin };
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Укажите корректный email."] });
            if (!allowedRoles.Contains(request.Role))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Через приглашение можно создать тренера, родителя, регионального аналитика или администратора. Игрок регистрируется самостоятельно."] });
            if (await users.FindByEmailAsync(email) is not null) return Results.Conflict(new { message = "Пользователь с таким email уже существует." });

            var region = request.Role == Roles.RegionalAnalyst ? request.Region?.Trim() : null;
            if (request.Role == Roles.RegionalAnalyst && (string.IsNullOrWhiteSpace(region) || !await db.Municipalities.AnyAsync(x => x.IsActive && x.Region == region)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["region"] = ["Для аналитика выберите регион из активного справочника городов."] });

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
            else if (request.Role == Roles.RegionalAnalyst)
                await users.AddClaimAsync(user, new Claim(KasanieClaimTypes.AnalyticsRegion, region!));

            await AddAudit(db, principal, "user_invited", nameof(ApplicationUser), user.Id, $"role:{request.Role};region:{region ?? "-"}");
            if (transaction is not null) await transaction.CommitAsync();

            var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(user));
            var inviteUrl = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");
            return Results.Created($"/api/admin/users/{user.Id}", new
            {
                user.Id,
                user.Email,
                role = request.Role,
                region,
                inviteUrl,
                expiresAt = DateTimeOffset.UtcNow.Add(tokenOptions.Value.TokenLifespan)
            });
        });

        admin.MapPost("/users/{id}/invite", async (string id, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db, IConfiguration configuration, IOptions<DataProtectionTokenProviderOptions> tokenOptions) =>
        {
            var target = await users.FindByIdAsync(id); if (target is null) return Results.NotFound();
            if (await users.HasPasswordAsync(target))
                return Results.Conflict(new { message = "Пользователь уже задал пароль — ему нужно восстановление, а не приглашение." });
            if (await users.IsLockedOutAsync(target))
                return Results.Conflict(new { message = "Учётная запись заблокирована. Сначала разблокируйте." });

            await AddAudit(db, principal, "user_invite_reissued", nameof(ApplicationUser), target.Id);
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
                roles = (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == x.Id select role.Name).ToList(),
                analyticsRegion = db.UserClaims.Where(c => c.UserId == x.Id && c.ClaimType == KasanieClaimTypes.AnalyticsRegion).Select(c => c.ClaimValue).FirstOrDefault()
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
            if (roleNames.Any(x => !Roles.All.Contains(x))) return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["Неизвестная роль."] });
            var target = await users.FindByIdAsync(id); if (target is null) return Results.NotFound();
            var current = await users.GetRolesAsync(target); await users.RemoveFromRolesAsync(target, current); await users.AddToRolesAsync(target, roleNames.Distinct());
            if (!roleNames.Contains(Roles.RegionalAnalyst))
            {
                var regionClaims = (await users.GetClaimsAsync(target)).Where(x => x.Type == KasanieClaimTypes.AnalyticsRegion).ToList();
                if (regionClaims.Count > 0) await users.RemoveClaimsAsync(target, regionClaims);
            }
            await users.UpdateSecurityStampAsync(target);
            await AddAudit(db, principal, "role_changed", nameof(ApplicationUser), id, string.Join(',', roleNames)); return Results.NoContent();
        });
        admin.MapPut("/users/{id}/analytics-region", async (string id, AnalystRegionRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var region = request.Region?.Trim();
            if (string.IsNullOrWhiteSpace(region) || !await db.Municipalities.AnyAsync(x => x.IsActive && x.Region == region))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["region"] = ["Выберите регион из активного справочника городов."] });
            var target = await users.FindByIdAsync(id); if (target is null) return Results.NotFound();
            if (!await users.IsInRoleAsync(target, Roles.RegionalAnalyst))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Сначала назначьте пользователю роль регионального аналитика."] });
            var currentClaims = (await users.GetClaimsAsync(target)).Where(x => x.Type == KasanieClaimTypes.AnalyticsRegion).ToList();
            if (currentClaims.Count > 0) await users.RemoveClaimsAsync(target, currentClaims);
            await users.AddClaimAsync(target, new Claim(KasanieClaimTypes.AnalyticsRegion, region));
            await users.UpdateSecurityStampAsync(target);
            await AddAudit(db, principal, "analyst_region_changed", nameof(ApplicationUser), id, region);
            return Results.NoContent();
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
