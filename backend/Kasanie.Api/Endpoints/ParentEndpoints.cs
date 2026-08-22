using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapParent(this IEndpointRouteBuilder app)
    {
        var parent = app.MapGroup("/api/parent").RequireAuthorization(Roles.Parent).WithTags("Parent");
        parent.MapGet("/children", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var children = await db.ParentPlayerLinks.AsNoTracking().Where(x => x.Parent.UserId == userId).Select(x => new
            {
                profile = new { x.Player.Id, x.Player.FirstName, x.Player.LastName, x.Player.DateOfBirth, x.Player.PreferredPosition, x.Player.ExperienceLevel },
                x.Relationship, x.IsPrimary, x.ConsentAccepted, x.ConsentVersion, x.ConsentAcceptedAt,
                completedWorkouts = db.TrainingSessions.Count(s => s.PlayerId == x.PlayerId && s.Status == SessionStatus.Completed)
            }).ToListAsync();
            return Results.Ok(children);
        });

        parent.MapPost("/children", async (ChildCreateRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = AgePolicy.GetAge(request.DateOfBirth, today);
            if (age < 0 || age >= 14) return Results.ValidationProblem(new Dictionary<string, string[]> { ["dateOfBirth"] = ["Через родительский кабинет создаются профили детей младше 14 лет."] });
            if (!request.ConsentAccepted || string.IsNullOrWhiteSpace(request.ConsentVersion)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["consentAccepted"] = ["Для создания детского профиля требуется зафиксировать согласие и его версию."] });
            var municipality = await ResolveCityAsync(db, request.City);
            if (municipality is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["city"] = ["Выберите город из подсказок."] });
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var parentProfile = await db.ParentProfiles.SingleAsync(x => x.UserId == userId);
            var player = new PlayerProfile { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DateOfBirth = request.DateOfBirth, MunicipalityId = municipality.Id, PreferredPosition = request.PreferredPosition, DominantFoot = request.DominantFoot, ExperienceLevel = request.ExperienceLevel };
            db.Players.Add(player);
            db.ParentPlayerLinks.Add(new ParentPlayerLink { Parent = parentProfile, Player = player, Relationship = request.Relationship, IsPrimary = true, ConsentAccepted = true, ConsentVersion = request.ConsentVersion, ConsentAcceptedAt = DateTimeOffset.UtcNow });
            db.AuditLogs.Add(new AuditLog { UserId = userId, EventType = "child_created", EntityType = nameof(PlayerProfile), Details = $"consent-version:{request.ConsentVersion}" });
            await db.SaveChangesAsync();
            return Results.Created($"/api/parent/children/{player.Id}", new { player.Id });
        });

        parent.MapGet("/children/{playerId:int}", async (int playerId, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            if (!await access.ParentCanAccessAsync(user, playerId)) return Results.Forbid();
            var player = await db.Players.AsNoTracking().Include(x => x.Municipality).SingleAsync(x => x.Id == playerId);
            var link = await db.ParentPlayerLinks.AsNoTracking().FirstAsync(x => x.PlayerId == playerId && x.Parent.UserId == user.FindFirstValue(ClaimTypes.NameIdentifier));
            var skills = await db.SkillSnapshots.AsNoTracking().Where(x => x.PlayerId == playerId).OrderByDescending(x => x.CapturedAt).Take(12).ToListAsync();
            var plan = await db.TrainingPlans.AsNoTracking().Where(x => x.PlayerId == playerId && x.Status == PlanStatus.Active).Include(x => x.Days).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).OrderByDescending(x => x.WeekStart).FirstOrDefaultAsync();
            var sessions = await db.TrainingSessions.AsNoTracking().Where(x => x.PlayerId == playerId).OrderByDescending(x => x.CompletedAt).Take(30).Select(x => new { x.Id, x.Status, x.StartedAt, x.CompletedAt, x.TrainingDay.Title }).ToListAsync();
            return Results.Ok(new { profile = PlayerDto(player), consent = new { link.ConsentAccepted, link.ConsentVersion, link.ConsentAcceptedAt }, skills = skills.Select(x => new { x.CapturedAt, values = SkillsDto(x) }), plan = plan is null ? null : PlanDto(plan, []), sessions });
        });

        parent.MapGet("/children/{playerId:int}/development", async (int playerId, ClaimsPrincipal user, IAccessService access, IPlayerDevelopmentService development) =>
        {
            if (!await access.ParentCanAccessAsync(user, playerId)) return Results.Forbid();
            return Results.Ok(await development.BuildAsync(playerId));
        });

        parent.MapPut("/children/{playerId:int}/consent", async (int playerId, ConsentRequest request, ClaimsPrincipal user, IAccessService access, AppDbContext db) =>
        {
            if (!await access.ParentCanAccessAsync(user, playerId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Version)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Версия согласия обязательна."] });
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var link = await db.ParentPlayerLinks.FirstAsync(x => x.PlayerId == playerId && x.Parent.UserId == userId);
            link.ConsentAccepted = request.Accepted; link.ConsentVersion = request.Version; link.ConsentAcceptedAt = request.Accepted ? DateTimeOffset.UtcNow : null;
            db.AuditLogs.Add(new AuditLog { UserId = userId, EventType = "consent_changed", EntityType = nameof(PlayerProfile), EntityId = playerId.ToString(), Details = $"accepted:{request.Accepted};version:{request.Version}" });
            await db.SaveChangesAsync(); return Results.NoContent();
        });
    }
}
