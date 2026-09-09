using System.Data;
using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapPublicDiscoveryReports(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/organizer/activities").RequireAuthorization().RequireRateLimiting("public-action")
            .WithTags("Sports Nearby — participant reports");
        api.MapPost("/{id:int}/participants/{participantId:long}/reports", FileParticipantReportAsync);
        api.MapDelete("/{id:int}/reports/{reportId:long}", RetractParticipantReportAsync);
        api.MapGet("/{id:int}/participants/{participantId:long}/reports", GetParticipantReportsAsync);
    }

    private static (string? SubjectUserId, string? SubjectGuestContactHash)? ReportSubjectKey(PublicActivityParticipant p) =>
        p.UserId is not null ? (p.UserId, (string?)null)
        : p.GuestContactHash is not null ? ((string?)null, p.GuestContactHash)
        : null;

    private static async Task<IResult> FileParticipantReportAsync(
        int id, long participantId, ParticipantReportRequest request, ClaimsPrincipal principal,
        AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!Enum.TryParse<ParticipantReportReason>(request.Reason, ignoreCase: false, out var reason) || !Enum.IsDefined(reason))
            return Results.ValidationProblem(Error("reason", "Выберите причину из списка."));
        var comment = request.Comment?.Trim() ?? string.Empty;
        if (comment.Length is < 1 or > 2000) return Results.ValidationProblem(Error("comment", "Комментарий от 1 до 2000 символов."));

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var activity = await db.PublicActivities.Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (activity is null) return Results.Forbid();
        if (activity.EndAt >= DateTimeOffset.UtcNow && activity.Status != PublicActivityStatus.Completed)
            return Results.Conflict(new { message = "Жалобу можно оставить только после завершения активности." });
        var participant = activity.Participants.SingleOrDefault(x => x.Id == participantId);
        if (participant is null) return Results.NotFound();
        var subject = ReportSubjectKey(participant);
        if (subject is null) return Results.Conflict(new { message = "У этого участника нет контакта для жалобы." });
        var (subjectUserId, subjectHash) = subject.Value;

        var duplicate = await db.PublicActivityParticipantReports.AnyAsync(r =>
            r.AuthorOrganizerId == userId && r.PublicActivityId == id && r.Status == ParticipantReportStatus.Active &&
            r.SubjectUserId == subjectUserId && r.SubjectGuestContactHash == subjectHash);
        if (duplicate) return Results.Conflict(new { message = "Вы уже оставляли жалобу на этого участника." });

        var report = new PublicActivityParticipantReport
        {
            PublicActivityId = id, PublicActivityParticipantId = participantId, AuthorOrganizerId = userId,
            SubjectUserId = subjectUserId, SubjectGuestContactHash = subjectHash, Reason = reason, Comment = comment
        };
        db.PublicActivityParticipantReports.Add(report);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "Вы уже оставляли жалобу на этого участника." }); }
        await audit.WriteAsync(userId, "participant_report_filed", nameof(PublicActivityParticipantReport), report.Id.ToString(), $"activity:{id};reason:{reason}");
        if (transaction is not null) await transaction.CommitAsync();
        return Results.Created($"/api/organizer/activities/{id}/participants/{participantId}/reports", new { reportId = report.Id });
    }

    private static async Task<IResult> RetractParticipantReportAsync(
        int id, long reportId, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var report = await db.PublicActivityParticipantReports.SingleOrDefaultAsync(x => x.Id == reportId && x.PublicActivityId == id);
        if (report is null) return Results.NotFound();
        if (report.AuthorOrganizerId != userId) return Results.Forbid();
        if (report.Status == ParticipantReportStatus.Retracted) return Results.NoContent();
        report.Status = ParticipantReportStatus.Retracted;
        report.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "participant_report_retracted", nameof(PublicActivityParticipantReport), report.Id.ToString());
        return Results.NoContent();
    }

    private static async Task<IResult> GetParticipantReportsAsync(
        int id, long participantId, ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var activity = await db.PublicActivities.AsNoTracking().Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (activity is null) return Results.Forbid();
        var participant = activity.Participants.SingleOrDefault(x => x.Id == participantId);
        if (participant is null) return Results.NotFound();
        var subject = ReportSubjectKey(participant);
        if (subject is null) return Results.Ok(Array.Empty<object>());
        var (subjectUserId, subjectHash) = subject.Value;

        var reports = await db.PublicActivityParticipantReports.AsNoTracking()
            .Where(r => r.Status == ParticipantReportStatus.Active &&
                r.SubjectUserId == subjectUserId && r.SubjectGuestContactHash == subjectHash)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.Reason, r.Comment, r.CreatedAt, r.AuthorOrganizerId, r.PublicActivityId })
            .ToListAsync();
        var activityIds = reports.Select(r => r.PublicActivityId).Distinct().ToArray();
        var titles = await db.PublicActivities.AsNoTracking()
            .Where(a => activityIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Title);
        return Results.Ok(reports.Select(r => new
        {
            reportId = r.Id,
            reason = r.Reason.ToString(),
            r.Comment,
            r.CreatedAt,
            isMine = r.AuthorOrganizerId == userId,
            sourceActivityTitle = titles.GetValueOrDefault(r.PublicActivityId, "")
        }).ToList());
    }

    public sealed record ParticipantReportRequest(string? Reason, string? Comment);
}
