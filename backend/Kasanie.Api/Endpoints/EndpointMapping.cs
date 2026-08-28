using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    public static IEndpointRouteBuilder MapKasanieEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuth();
        app.MapPlayer();
        app.MapCoach();
        app.MapTeamTraining();
        app.MapParent();
        app.MapAnalytics();
        app.MapAdmin();
        app.MapSchools();
        app.MapPublicDiscovery();
        return app;
    }

    private static async Task<Municipality?> ResolveCityAsync(AppDbContext db, string? city)
    {
        var normalized = NormalizeCity(city);
        if (normalized.Length == 0) return null;
        var normalizedLower = normalized.ToLower();
        return await db.Municipalities.FirstOrDefaultAsync(x => x.IsActive && x.Name.ToLower() == normalizedLower);
    }

    private static string NormalizeCity(string? city)
    {
        var value = city?.Trim() ?? string.Empty;
        if (value.StartsWith("г.", StringComparison.OrdinalIgnoreCase)) value = value[2..].Trim();
        if (value.StartsWith("город ", StringComparison.OrdinalIgnoreCase)) value = value[6..].Trim();
        return value;
    }
}
