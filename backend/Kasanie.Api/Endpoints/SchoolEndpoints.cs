using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapSchools(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/schools").RequireAuthorization(Roles.Admin).WithTags("Schools admin");

        admin.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Schools.AsNoTracking().OrderBy(x => x.Name).Select(x => new
        {
            x.Id, x.Name, x.Slug, x.City, x.ContactEmail, x.Phone, x.IsActive, x.CreatedAt,
            ownerEmail = db.SchoolMemberships.Where(m => m.SchoolId == x.Id && m.Role == SchoolMembershipRole.Owner && m.IsActive).Select(m => m.User.Email).FirstOrDefault(),
            teams = db.Teams.Count(t => t.SchoolId == x.Id && t.IsActive),
            coaches = db.SchoolMemberships.Count(m => m.SchoolId == x.Id && m.Role == SchoolMembershipRole.Coach && m.IsActive),
            players = db.TeamPlayers.Where(p => p.Team.SchoolId == x.Id && p.IsActive).Select(p => p.PlayerId).Distinct().Count()
        }).ToListAsync()));

        admin.MapPost("/", async (SchoolCreateRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db, IConfiguration configuration, IOptions<DataProtectionTokenProviderOptions> tokenOptions) =>
        {
            var errors = SchoolErrors(request.Name, request.OwnerEmail);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var ownerEmail = request.OwnerEmail.Trim().ToLowerInvariant();
            var owner = await users.FindByEmailAsync(ownerEmail);
            var createdOwner = owner is null;
            if (owner is null)
            {
                owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
                var createResult = await users.CreateAsync(owner);
                if (!createResult.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["ownerEmail"] = createResult.Errors.Select(x => x.Description).ToArray() });
            }
            if (!await users.IsInRoleAsync(owner, Roles.SchoolOwner)) await users.AddToRoleAsync(owner, Roles.SchoolOwner);

            var school = new School
            {
                Name = request.Name.Trim(), Slug = await UniqueSlugAsync(db, request.Name), City = Clean(request.City),
                ContactEmail = Clean(request.ContactEmail), Phone = Clean(request.Phone)
            };
            db.Schools.Add(school);
            db.SchoolMemberships.Add(new SchoolMembership { School = school, UserId = owner.Id, Role = SchoolMembershipRole.Owner });
            await db.SaveChangesAsync();
            await AddAudit(db, principal, "school_created", nameof(School), school.Id.ToString(), $"owner={owner.Email}");

            string? inviteUrl = null;
            DateTimeOffset? expiresAt = null;
            if (createdOwner || !await users.HasPasswordAsync(owner))
            {
                var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(owner));
                inviteUrl = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(owner.Email!)}&token={Uri.EscapeDataString(token)}");
                expiresAt = DateTimeOffset.UtcNow.Add(tokenOptions.Value.TokenLifespan);
            }
            return Results.Created($"/api/admin/schools/{school.Id}", new { school.Id, school.Name, school.Slug, ownerEmail = owner.Email, inviteUrl, expiresAt });
        });

        admin.MapPut("/{id:int}/status", async (int id, SchoolStatusRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var school = await db.Schools.FindAsync(id); if (school is null) return Results.NotFound();
            school.IsActive = request.IsActive; school.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await AddAudit(db, principal, request.IsActive ? "school_unblocked" : "school_blocked", nameof(School), id.ToString());
            return Results.NoContent();
        });

        var portal = app.MapGroup("/api/school").RequireAuthorization(policy => policy.RequireRole(Roles.SchoolOwner, Roles.SchoolAdmin)).WithTags("School portal");
        portal.MapGet("/memberships", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return Results.Ok(await db.SchoolMemberships.AsNoTracking().Where(x => x.UserId == userId && x.IsActive && x.School.IsActive && x.Role != SchoolMembershipRole.Coach)
                .OrderBy(x => x.School.Name).Select(x => new { x.SchoolId, x.School.Name, role = x.Role.ToString() }).ToListAsync());
        });

        portal.MapGet("/{schoolId:int}/overview", async (int schoolId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            var item = await db.Schools.AsNoTracking().Where(x => x.Id == schoolId).Select(x => new
            {
                x.Id, x.Name, x.Slug, x.City, x.ContactEmail, x.Phone, x.LogoUrl, x.IsActive,
                teams = db.Teams.Count(t => t.SchoolId == schoolId && t.IsActive),
                coaches = db.SchoolMemberships.Count(m => m.SchoolId == schoolId && m.Role == SchoolMembershipRole.Coach && m.IsActive),
                players = db.TeamPlayers.Where(p => p.Team.SchoolId == schoolId && p.IsActive).Select(p => p.PlayerId).Distinct().Count()
            }).SingleAsync();
            return Results.Ok(item);
        });

        portal.MapPut("/{schoolId:int}/settings", async (int schoolId, SchoolUpdateRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Укажите название школы."] });
            var item = await db.Schools.FindAsync(schoolId); if (item is null) return Results.NotFound();
            item.Name = request.Name.Trim(); item.City = Clean(request.City); item.ContactEmail = Clean(request.ContactEmail); item.Phone = Clean(request.Phone); item.LogoUrl = Clean(request.LogoUrl); item.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); await AddAudit(db, principal, "school_updated", nameof(School), schoolId.ToString());
            return Results.NoContent();
        });

        portal.MapGet("/{schoolId:int}/teams", async (int schoolId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            return Results.Ok(await db.Teams.AsNoTracking().Where(x => x.SchoolId == schoolId).OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).Select(x => new
            {
                x.Id, x.Name, x.AgeGroup, x.Season, x.IsActive,
                coaches = x.TeamCoaches.Select(c => new { c.CoachId, c.Coach.DisplayName, c.IsHeadCoach }),
                players = x.TeamPlayers.Count(p => p.IsActive)
            }).ToListAsync());
        });

        portal.MapPost("/{schoolId:int}/teams", async (int schoolId, TeamUpsertRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Укажите название команды."] });
            var team = new Team { SchoolId = schoolId, Name = request.Name.Trim(), AgeGroup = Clean(request.AgeGroup), Season = Clean(request.Season), IsActive = request.IsActive };
            db.Teams.Add(team); await db.SaveChangesAsync(); await AddAudit(db, principal, "team_created", nameof(Team), team.Id.ToString(), $"school={schoolId}");
            return Results.Created($"/api/school/{schoolId}/teams/{team.Id}", new { team.Id });
        });

        portal.MapPut("/{schoolId:int}/teams/{teamId:int}", async (int schoolId, int teamId, TeamUpsertRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.SchoolId == schoolId); if (team is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Укажите название команды."] });
            team.Name = request.Name.Trim(); team.AgeGroup = Clean(request.AgeGroup); team.Season = Clean(request.Season); team.IsActive = request.IsActive; team.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); await AddAudit(db, principal, "team_updated", nameof(Team), team.Id.ToString()); return Results.NoContent();
        });

        portal.MapGet("/{schoolId:int}/coaches", async (int schoolId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            return Results.Ok(await db.SchoolMemberships.AsNoTracking().Where(x => x.SchoolId == schoolId && x.Role == SchoolMembershipRole.Coach).OrderBy(x => x.User.Email).Select(x => new
            {
                x.UserId, x.User.Email, x.IsActive,
                coachId = db.CoachProfiles.Where(c => c.UserId == x.UserId).Select(c => (int?)c.Id).FirstOrDefault(),
                displayName = db.CoachProfiles.Where(c => c.UserId == x.UserId).Select(c => c.DisplayName).FirstOrDefault()
            }).ToListAsync());
        });

        portal.MapPost("/{schoolId:int}/coaches", async (int schoolId, SchoolCoachInviteRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db, IConfiguration configuration, IOptions<DataProtectionTokenProviderOptions> tokenOptions) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            if (!new EmailAddressAttribute().IsValid(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["coach"] = ["Укажите имя и корректный email тренера."] });
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await users.FindByEmailAsync(email);
            var created = user is null;
            if (user is null)
            {
                user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                var result = await users.CreateAsync(user);
                if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = result.Errors.Select(x => x.Description).ToArray() });
            }
            if (await db.SchoolMemberships.AnyAsync(x => x.SchoolId != schoolId && x.UserId == user.Id && x.Role == SchoolMembershipRole.Coach && x.IsActive))
                return Results.Conflict(new { message = "Этот тренер уже состоит в другой школе." });
            if (!await users.IsInRoleAsync(user, Roles.Coach)) await users.AddToRoleAsync(user, Roles.Coach);
            var profile = await db.CoachProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id);
            if (profile is null) { profile = new CoachProfile { UserId = user.Id, DisplayName = request.DisplayName.Trim() }; db.CoachProfiles.Add(profile); }
            else profile.DisplayName = request.DisplayName.Trim();
            var membership = await db.SchoolMemberships.SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.UserId == user.Id);
            if (membership is null) db.SchoolMemberships.Add(new SchoolMembership { SchoolId = schoolId, UserId = user.Id, Role = SchoolMembershipRole.Coach });
            else { membership.Role = SchoolMembershipRole.Coach; membership.IsActive = true; }
            await db.SaveChangesAsync(); await AddAudit(db, principal, "school_coach_invited", nameof(SchoolMembership), $"{schoolId}:{user.Id}");
            string? inviteUrl = null; DateTimeOffset? expiresAt = null;
            if (created || !await users.HasPasswordAsync(user))
            {
                var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(user));
                inviteUrl = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}");
                expiresAt = DateTimeOffset.UtcNow.Add(tokenOptions.Value.TokenLifespan);
            }
            return Results.Ok(new { profile.Id, user.Email, inviteUrl, expiresAt });
        });

        portal.MapPost("/{schoolId:int}/teams/{teamId:int}/coaches", async (int schoolId, int teamId, TeamCoachRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            if (!await db.Teams.AnyAsync(x => x.Id == teamId && x.SchoolId == schoolId)) return Results.NotFound();
            if (!await db.SchoolMemberships.AnyAsync(x => x.SchoolId == schoolId && x.IsActive && x.Role == SchoolMembershipRole.Coach && x.UserId == db.CoachProfiles.Where(c => c.Id == request.CoachId).Select(c => c.UserId).FirstOrDefault())) return Results.ValidationProblem(new Dictionary<string, string[]> { ["coachId"] = ["Тренер не состоит в этой школе."] });
            var link = await db.TeamCoaches.SingleOrDefaultAsync(x => x.TeamId == teamId && x.CoachId == request.CoachId);
            if (link is null) db.TeamCoaches.Add(new TeamCoach { TeamId = teamId, CoachId = request.CoachId, IsHeadCoach = request.IsHeadCoach }); else link.IsHeadCoach = request.IsHeadCoach;
            await db.SaveChangesAsync(); await AddAudit(db, principal, "team_coach_assigned", nameof(TeamCoach), $"{teamId}:{request.CoachId}"); return Results.NoContent();
        });

        portal.MapDelete("/{schoolId:int}/teams/{teamId:int}/coaches/{coachId:int}", async (int schoolId, int teamId, int coachId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            var link = await db.TeamCoaches.SingleOrDefaultAsync(x => x.TeamId == teamId && x.Team.SchoolId == schoolId && x.CoachId == coachId); if (link is null) return Results.NotFound();
            db.TeamCoaches.Remove(link); await db.SaveChangesAsync(); await AddAudit(db, principal, "team_coach_removed", nameof(TeamCoach), $"{teamId}:{coachId}"); return Results.NoContent();
        });

        portal.MapGet("/{schoolId:int}/players", async (int schoolId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            return Results.Ok(await db.TeamPlayers.AsNoTracking().Where(x => x.Team.SchoolId == schoolId && x.IsActive).OrderBy(x => x.Player.LastName).Select(x => new
            {
                x.PlayerId, x.Player.FirstName, x.Player.LastName, x.Player.DateOfBirth, city = x.Player.Municipality.Name, x.Player.PreferredPosition,
                x.TeamId, team = x.Team.Name, x.ShirtNumber
            }).ToListAsync());
        });

        portal.MapPost("/{schoolId:int}/players", async (int schoolId, SchoolPlayerCreateRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            var municipality = await ResolveCityAsync(db, request.City);
            if (municipality is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["city"] = ["Выберите город из подсказок."] });
            if (!await db.Teams.AnyAsync(x => x.Id == request.TeamId && x.SchoolId == schoolId && x.IsActive)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["teamId"] = ["Выберите активную команду школы."] });
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Укажите имя и фамилию игрока."] });
            var player = new PlayerProfile { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DateOfBirth = request.DateOfBirth, MunicipalityId = municipality.Id, PreferredPosition = request.PreferredPosition.Trim(), DominantFoot = request.DominantFoot.Trim(), ExperienceLevel = request.ExperienceLevel.Trim() };
            db.Players.Add(player); db.TeamPlayers.Add(new TeamPlayer { TeamId = request.TeamId, Player = player, ShirtNumber = request.ShirtNumber });
            await db.SaveChangesAsync(); await AddAudit(db, principal, "school_player_created", nameof(PlayerProfile), player.Id.ToString(), $"school={schoolId};team={request.TeamId}");
            return Results.Created($"/api/school/{schoolId}/players/{player.Id}", new { player.Id });
        });

        portal.MapPost("/{schoolId:int}/teams/{teamId:int}/players", async (int schoolId, int teamId, TeamPlayerRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            if (!await db.Teams.AnyAsync(x => x.Id == teamId && x.SchoolId == schoolId && x.IsActive)) return Results.NotFound();
            var belongsElsewhere = await db.TeamPlayers.AnyAsync(x => x.PlayerId == request.PlayerId && x.IsActive && x.Team.SchoolId != schoolId);
            if (belongsElsewhere) return Results.Conflict(new { message = "Игрок уже состоит в другой школе." });
            if (!await db.Players.AnyAsync(x => x.Id == request.PlayerId)) return Results.NotFound();
            var link = await db.TeamPlayers.SingleOrDefaultAsync(x => x.TeamId == teamId && x.PlayerId == request.PlayerId);
            if (link is null) db.TeamPlayers.Add(new TeamPlayer { TeamId = teamId, PlayerId = request.PlayerId, ShirtNumber = request.ShirtNumber });
            else { link.IsActive = true; link.LeftAt = null; link.ShirtNumber = request.ShirtNumber; }
            await db.SaveChangesAsync(); await AddAudit(db, principal, "team_player_assigned", nameof(TeamPlayer), $"{teamId}:{request.PlayerId}"); return Results.NoContent();
        });

        portal.MapDelete("/{schoolId:int}/teams/{teamId:int}/players/{playerId:int}", async (int schoolId, int teamId, int playerId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!await CanManageSchoolAsync(db, principal, schoolId)) return Results.Forbid();
            var link = await db.TeamPlayers.SingleOrDefaultAsync(x => x.TeamId == teamId && x.Team.SchoolId == schoolId && x.PlayerId == playerId && x.IsActive); if (link is null) return Results.NotFound();
            link.IsActive = false; link.LeftAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); await AddAudit(db, principal, "team_player_removed", nameof(TeamPlayer), $"{teamId}:{playerId}"); return Results.NoContent();
        });
    }

    private static Task<bool> CanManageSchoolAsync(AppDbContext db, ClaimsPrincipal principal, int schoolId)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return db.SchoolMemberships.AnyAsync(x => x.SchoolId == schoolId && x.UserId == userId && x.IsActive && x.School.IsActive && x.Role != SchoolMembershipRole.Coach);
    }

    private static Dictionary<string, string[]> SchoolErrors(string name, string ownerEmail)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Укажите название школы."];
        if (string.IsNullOrWhiteSpace(ownerEmail) || !new EmailAddressAttribute().IsValid(ownerEmail)) errors["ownerEmail"] = ["Укажите корректный email владельца."];
        return errors;
    }

    private static async Task<string> UniqueSlugAsync(AppDbContext db, string name)
    {
        var value = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-zа-яё0-9]+", "-").Trim('-');
        if (value.Length == 0) value = "school";
        var candidate = value;
        for (var suffix = 2; await db.Schools.AnyAsync(x => x.Slug == candidate); suffix++) candidate = $"{value}-{suffix}";
        return candidate;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
