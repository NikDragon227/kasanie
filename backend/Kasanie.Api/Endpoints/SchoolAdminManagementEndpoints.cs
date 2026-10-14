using System.ComponentModel.DataAnnotations;
using System.Data;
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
    public sealed record SchoolOwnerChangeRequest(string OwnerEmail, string? ExpectedOwnerEmail = null);
    private static void MapSchoolAdminManagement(RouteGroupBuilder admin)
    {
        admin.MapPut("/{schoolId:int}/owner", async (int schoolId, SchoolOwnerChangeRequest request, ClaimsPrincipal principal, AppDbContext db, UserManager<ApplicationUser> users, IConfiguration configuration, IOptions<DataProtectionTokenProviderOptions> tokenOptions) =>
        {
            if (string.IsNullOrWhiteSpace(request.OwnerEmail) || request.OwnerEmail.Length > 254 || !new EmailAddressAttribute().IsValid(request.OwnerEmail))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ownerEmail"] = ["Укажите корректный email нового владельца."] });
            var school = await db.Schools.FindAsync(schoolId); if (school is null) return Results.NotFound();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            try
            {
                var former = await db.SchoolMemberships.Include(x => x.User).Where(x => x.SchoolId == schoolId && x.Role == SchoolMembershipRole.Owner && x.IsActive).ToListAsync();
                if (request.ExpectedOwnerEmail is not null && !former.Any(x => string.Equals(x.User.Email, request.ExpectedOwnerEmail, StringComparison.OrdinalIgnoreCase)))
                    return Results.Conflict(new { message = "Владелец уже изменён. Обновите список школ перед назначением." });
                var email = request.OwnerEmail.Trim().ToLowerInvariant();
                var owner = await users.FindByEmailAsync(email);
                var created = owner is null;
                if (owner is null)
                {
                    owner = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = true };
                    var result = await users.CreateAsync(owner);
                    if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["ownerEmail"] = result.Errors.Select(x => x.Description).ToArray() });
                }
                var grant = await users.AddToRoleAsync(owner, Roles.SchoolOwner);
                if (!grant.Succeeded && !await users.IsInRoleAsync(owner, Roles.SchoolOwner))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["ownerEmail"] = grant.Errors.Select(x => x.Description).ToArray() });
                foreach (var previous in former.Where(x => x.UserId != owner.Id))
                {
                    previous.IsActive = false;
                    previous.Role = await db.CoachProfiles.AnyAsync(x => x.UserId == previous.UserId) ? SchoolMembershipRole.Coach : SchoolMembershipRole.Administrator;
                }
                var membership = await db.SchoolMemberships.SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.UserId == owner.Id);
                if (membership is null) db.SchoolMemberships.Add(new SchoolMembership { SchoolId = schoolId, UserId = owner.Id, Role = SchoolMembershipRole.Owner });
                else { membership.Role = SchoolMembershipRole.Owner; membership.IsActive = true; }
                school.UpdatedAt = DateTimeOffset.UtcNow;
                db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "school_owner_changed", EntityType = nameof(School), EntityId = schoolId.ToString(), Details = $"former={string.Join(',', former.Select(x => x.UserId))};owner={owner.Id}" });
                await db.SaveChangesAsync();
                foreach (var previous in former.Where(x => x.UserId != owner.Id))
                {
                    if (!await db.SchoolMemberships.AnyAsync(x => x.UserId == previous.UserId && x.Role == SchoolMembershipRole.Owner && x.IsActive) && await users.IsInRoleAsync(previous.User, Roles.SchoolOwner))
                    {
                        var revoke = await users.RemoveFromRoleAsync(previous.User, Roles.SchoolOwner);
                        if (!revoke.Succeeded) throw new InvalidOperationException("Не удалось обновить роль прежнего владельца.");
                    }
                    await users.UpdateSecurityStampAsync(previous.User);
                }
                if (transaction is not null) await transaction.CommitAsync();
                string? inviteUrl = null; DateTimeOffset? expiresAt = null;
                if (created || !await users.HasPasswordAsync(owner))
                {
                    var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(owner));
                    inviteUrl = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(owner.Email!)}&token={Uri.EscapeDataString(token)}");
                    expiresAt = DateTimeOffset.UtcNow.Add(tokenOptions.Value.TokenLifespan);
                }
                return Results.Ok(new { school.Id, ownerEmail = owner.Email, inviteUrl, expiresAt });
            }
            catch (DbUpdateException) { return Results.Conflict(new { message = "Данные школы изменились параллельно. Обновите страницу." }); }
            catch (Npgsql.PostgresException exception) when (exception.SqlState is "40001" or "40P01") { return Results.Conflict(new { message = "Владелец изменяется другим администратором. Обновите страницу." }); }
        });

        admin.MapPut("/{schoolId:int}/coaches/{userId}/status", async (int schoolId, string userId, SchoolStatusRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var membership = await db.SchoolMemberships.SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.UserId == userId);
            if (membership is null) return Results.NotFound();
            if (membership.Role == SchoolMembershipRole.Owner) return Results.Conflict(new { message = "Доступ владельца меняется через смену владельца школы." });
            if (!await db.CoachProfiles.AnyAsync(x => x.UserId == userId)) return Results.BadRequest(new { message = "У пользователя нет профиля тренера." });
            membership.IsActive = request.IsActive; membership.Role = SchoolMembershipRole.Coach;
            if (!request.IsActive)
                db.TeamCoaches.RemoveRange(await db.TeamCoaches.Where(x => x.Team.SchoolId == schoolId && x.Coach.UserId == userId).ToListAsync());
            await SaveCoachAudit(db, principal, request.IsActive ? "admin_school_coach_enabled" : "admin_school_coach_disabled", nameof(SchoolMembership), $"{schoolId}:{userId}");
            return Results.NoContent();
        });

        admin.MapPut("/{schoolId:int}/teams/{teamId:int}/cycle", async (int schoolId, int teamId, TeamCycleRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.SchoolId == schoolId); if (team is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Stage) || request.StartsOn.HasValue && request.EndsOn.HasValue && request.StartsOn > request.EndsOn)
                return Results.BadRequest(new { message = "Проверьте этап и даты цикла." });
            team.TrainingCycleStage = request.Stage.Trim(); team.CycleStart = request.StartsOn; team.CycleEnd = request.EndsOn; team.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveCoachAudit(db, principal, "admin_team_cycle_updated", nameof(Team), team.Id.ToString()); return Results.NoContent();
        });
    }
}
