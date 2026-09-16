using System.Security.Claims;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapAnalytics(this IEndpointRouteBuilder app)
    {
        var analytics = app.MapGroup("/api/analytics").WithTags("Analytics");
        analytics.MapPost("/events", async (ProductAnalyticsEventRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var errors = Validation.Analytics(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            db.ProductAnalyticsEvents.Add(new ProductAnalyticsEvent
            {
                UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier),
                Name = request.Name!.Trim(),
                SessionId = request.SessionId!.Trim(),
                PagePath = request.PagePath!.Trim(),
                PropertiesJson = request.Properties?.GetRawText()
            });
            await db.SaveChangesAsync();
            return Results.Accepted();
        }).RequireRateLimiting("analytics");
    }
}
