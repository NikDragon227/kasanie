using System.Security.Claims;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapFeedback(this IEndpointRouteBuilder app)
    {
        var feedback = app.MapGroup("/api/feedback").WithTags("Feedback");
        feedback.MapPost("", async (FeedbackCreateRequest request, ClaimsPrincipal principal, AppDbContext db, ITransactionalEmailSender emailSender, IOptions<EmailOptions> emailOptions, ILoggerFactory loggerFactory) =>
        {
            var errors = Validation.Feedback(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var category = Enum.Parse<FeedbackCategory>(request.Category!, ignoreCase: false);

            var submission = new FeedbackSubmission
            {
                UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier),
                Category = category,
                Message = request.Message.Trim(),
                ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim().ToLowerInvariant(),
                PagePath = request.PagePath.Trim(),
                TechnicalContext = string.IsNullOrWhiteSpace(request.TechnicalContext) ? null : request.TechnicalContext.Trim()
            };
            db.FeedbackSubmissions.Add(submission);
            await db.SaveChangesAsync();
            var supportInbox = emailOptions.Value.SupportInbox.Trim();
            if (!string.IsNullOrWhiteSpace(supportInbox))
            {
                try
                {
                    var (subject, html, text) = EmailTemplates.SupportFeedback(category.ToString(), submission.Message, submission.PagePath, submission.ContactEmail);
                    await emailSender.SendAsync(supportInbox, subject, html, text);
                }
                catch (Exception exception)
                {
                    loggerFactory.CreateLogger("Kasanie.Api.Feedback.Email").LogError(exception, "Не удалось уведомить поддержку о feedback {FeedbackId}", submission.Id);
                }
            }
            return Results.Created($"/api/feedback/{submission.Id}", new { submission.Id, status = submission.Status.ToString() });
        }).RequireRateLimiting("public-feedback");

        MapAdminFeedback(app);
    }

    private static void MapAdminFeedback(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/feedback").RequireAuthorization(Roles.Admin).WithTags("Admin");
        admin.MapGet("", async (FeedbackStatus? status, FeedbackCategory? category, int page, int pageSize, AppDbContext db) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 30 : pageSize, 1, 100);
            var query = db.FeedbackSubmissions.AsNoTracking();
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (category.HasValue) query = query.Where(x => x.Category == category.Value);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    category = x.Category.ToString(),
                    x.Message,
                    x.ContactEmail,
                    x.PagePath,
                    x.TechnicalContext,
                    status = x.Status.ToString(),
                    priority = x.Priority.ToString(),
                    x.ResolutionNote,
                    x.CreatedAt,
                    x.UpdatedAt,
                    userEmail = x.UserId == null ? null : db.Users.Where(user => user.Id == x.UserId).Select(user => user.Email).FirstOrDefault()
                }).ToListAsync();
            return Results.Ok(new { total, page, pageSize, items });
        });

        admin.MapPut("/{id:long}", async (long id, FeedbackUpdateRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!Enum.TryParse<FeedbackStatus>(request.Status, ignoreCase: false, out var status) || !Enum.IsDefined(status))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Выберите корректный статус."] });
            if (!Enum.TryParse<FeedbackPriority>(request.Priority, ignoreCase: false, out var priority) || !Enum.IsDefined(priority))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["priority"] = ["Выберите корректный приоритет."] });
            if (request.ResolutionNote?.Length > 2000)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["resolutionNote"] = ["Комментарий не должен превышать 2000 символов."] });
            var submission = await db.FeedbackSubmissions.FindAsync(id);
            if (submission is null) return Results.NotFound();
            submission.Status = status;
            submission.Priority = priority;
            submission.ResolutionNote = string.IsNullOrWhiteSpace(request.ResolutionNote) ? null : request.ResolutionNote.Trim();
            submission.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await AddAudit(db, principal, "feedback_updated", nameof(FeedbackSubmission), id.ToString(), $"status:{status};priority:{priority}");
            return Results.NoContent();
        });
    }
}
