using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kasanie.Api.Application;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static readonly IReadOnlyDictionary<string, string[]> GameFormatsBySport = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["football"] = ["5×5", "6×6", "7×7", "8×8", "9×9", "10×10", "11×11"],
        ["basketball"] = ["3×3", "5×5"],
        ["volleyball"] = ["2×2", "6×6"],
        ["hockey"] = ["3+1", "5+1"],
        ["tennis"] = ["1×1", "2×2"],
        ["badminton"] = ["1×1", "2×2"]
    };

    private static void MapPublicDiscovery(this IEndpointRouteBuilder app)
    {
        var publicApi = app.MapGroup("/api/public").RequireRateLimiting("public-discovery").WithTags("Sports Nearby — public catalog");

        publicApi.MapGet("/platform-stats", async (AppDbContext db) =>
        {
            var users = await db.Users.AsNoTracking().CountAsync();
            var teams = await db.Teams.AsNoTracking().CountAsync(x => x.IsActive);
            var teamTournaments = await db.TeamTournaments.AsNoTracking().CountAsync();
            var publicTournaments = await db.PublicActivities.AsNoTracking().CountAsync(x =>
                x.EventType == PublicActivityType.Tournament &&
                x.Status != PublicActivityStatus.Draft &&
                x.Status != PublicActivityStatus.Cancelled &&
                x.Status != PublicActivityStatus.Archived);
            var coaches = await db.CoachProfiles.AsNoTracking().CountAsync();

            return Results.Ok(new
            {
                users,
                teams,
                tournaments = teamTournaments + publicTournaments,
                coaches,
                trustPercent = (int?)null
            });
        });

        publicApi.MapGet("/sports", async (AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            return Results.Ok(await db.Sports.AsNoTracking().Where(x => x.IsActive && x.Slug != "futsal" && x.Slug != "mini-football" && x.Name != "Мини-футбол").OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Slug, x.Name }).ToListAsync());
        });

        publicApi.MapGet("/activities", SearchPublicActivitiesAsync);
        publicApi.MapGet("/geocode", GeocodePublicLocationAsync);
        publicApi.MapPost("/activities/{id:int}/guest-join", JoinGuestPublicActivityAsync);
        publicApi.MapGet("/guest-participations/{token}", GetGuestPublicParticipationAsync);
        publicApi.MapPost("/guest-participations/{token}/cancel", CancelGuestPublicParticipationAsync);

        publicApi.MapGet("/activities/{slug}", async (string slug, ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            var activity = await db.PublicActivities.AsNoTracking()
                .Include(x => x.Sport).Include(x => x.Venue).Include(x => x.Participants)
                .SingleOrDefaultAsync(x => x.Slug == slug && x.Visibility == PublicActivityVisibility.Public &&
                    (x.Status == PublicActivityStatus.Published || x.Status == PublicActivityStatus.Full));
            if (activity is null) return Results.NotFound();
            var organizerName = await db.PublicOrganizerProfiles.AsNoTracking().Where(x => x.UserId == activity.OrganizerId).Select(x => x.DisplayName).SingleOrDefaultAsync();
            return Results.Ok(ToPublicActivity(activity, organizerName, principal.FindFirstValue(ClaimTypes.NameIdentifier)));
        });

        publicApi.MapGet("/venues", async (string? location, AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            var query = db.SportsVenues.AsNoTracking().Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(location))
            {
                var normalized = location.Trim().ToLower();
                query = query.Where(x => x.City.ToLower().Contains(normalized) ||
                    (x.District != null && x.District.ToLower().Contains(normalized)));
            }
            var venues = await query.OrderBy(x => x.City).ThenBy(x => x.Name).Take(100).ToListAsync();
            return Results.Ok(venues.Select(ToPublicVenue));
        });

        publicApi.MapGet("/venues/{slug}", async (string slug, AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            var venue = await db.SportsVenues.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug && x.IsActive);
            return venue is null ? Results.NotFound() : Results.Ok(ToPublicVenue(venue));
        });

        var participantApi = app.MapGroup("/api/activities").RequireAuthorization().RequireRateLimiting("public-action").WithTags("Sports Nearby — participation");
        participantApi.MapGet("/mine", GetMyPublicActivitiesAsync);
        participantApi.MapGet("/{id:int}/participation", GetPublicActivityParticipationAsync);
        participantApi.MapPost("/{id:int}/join", JoinPublicActivityAsync);
        participantApi.MapPost("/{id:int}/leave", LeavePublicActivityAsync);

        var organizerApi = app.MapGroup("/api/organizer/activities").RequireAuthorization().RequireRateLimiting("public-action").WithTags("Sports Nearby — organizer");
        organizerApi.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var items = await db.PublicActivities.AsNoTracking().Include(x => x.Sport).Include(x => x.Venue).Include(x => x.Participants)
                .Where(x => x.OrganizerId == userId && x.Status != PublicActivityStatus.Archived).OrderByDescending(x => x.StartAt).ToListAsync();
            var organizerName = await db.PublicOrganizerProfiles.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.DisplayName).SingleOrDefaultAsync();
            return Results.Ok(items.Select(x => ToPublicActivity(x, organizerName)));
        });
        organizerApi.MapPost("/", CreatePublicActivityAsync);
        organizerApi.MapPut("/{id:int}", UpdatePublicActivityAsync);
        organizerApi.MapPost("/{id:int}/publish", PublishPublicActivityAsync);
        organizerApi.MapPost("/{id:int}/cancel", CancelPublicActivityAsync);
        organizerApi.MapDelete("/{id:int}", DeletePublicActivityAsync);
        organizerApi.MapGet("/{id:int}/participants", GetOrganizerParticipantsAsync);
        organizerApi.MapDelete("/{id:int}/participants/{participantId:long}", RemoveOrganizerParticipantAsync);

        var organizerVenueApi = app.MapGroup("/api/organizer/venues").RequireAuthorization().RequireRateLimiting("public-action").WithTags("Sports Nearby — organizer venues");
        organizerVenueApi.MapPost("/", CreatePublicVenueAsync);
    }

    private static async Task<IResult> SearchPublicActivitiesAsync(
        string? sport, string? gameFormat, string? city, string? district, string? location, DateOnly? date, TimeOnly? time, PublicActivityType? type, bool? freeOnly,
        bool? availableOnly, double? latitude, double? longitude, double? radiusKm, string? sort,
        AppDbContext db, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var query = db.PublicActivities.AsNoTracking().Include(x => x.Sport).Include(x => x.Venue).Include(x => x.Participants)
            .Where(x => x.Visibility == PublicActivityVisibility.Public &&
                (x.Status == PublicActivityStatus.Published || x.Status == PublicActivityStatus.Full) && x.EndAt > DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(sport)) query = query.Where(x => x.Sport.Slug == sport.Trim().ToLower());
        if (!string.IsNullOrWhiteSpace(gameFormat)) query = query.Where(x => x.GameFormat == gameFormat.Trim());
        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim().ToLower();
            query = query.Where(x => x.Venue.City.ToLower().Contains(normalizedCity));
        }
        if (!string.IsNullOrWhiteSpace(district))
        {
            var normalizedDistrict = district.Trim().ToLower();
            query = query.Where(x => x.Venue.District != null && x.Venue.District.ToLower().Contains(normalizedDistrict));
        }
        if (!string.IsNullOrWhiteSpace(location))
        {
            var normalized = location.Trim().ToLower();
            query = query.Where(x => x.Venue.City.ToLower().Contains(normalized) ||
                (x.Venue.District != null && x.Venue.District.ToLower().Contains(normalized)) ||
                x.Venue.Address.ToLower().Contains(normalized));
        }
        if (date.HasValue)
        {
            var from = new DateTimeOffset(date.Value.ToDateTime(time ?? TimeOnly.MinValue), TimeSpan.Zero);
            var to = new DateTimeOffset(date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.StartAt >= from && x.StartAt < to);
        }
        else if (time.HasValue)
        {
            var minimumTime = time.Value.ToTimeSpan();
            query = query.Where(x => x.StartAt.TimeOfDay >= minimumTime);
        }
        if (type.HasValue) query = query.Where(x => x.EventType == type.Value);
        if (freeOnly == true) query = query.Where(x => x.Price == 0);

        var effectiveRadius = Math.Clamp(radiusKm ?? 10, 1, 100);
        if (latitude.HasValue && longitude.HasValue)
        {
            var latitudeDelta = effectiveRadius / 111d;
            var longitudeDelta = effectiveRadius / Math.Max(25d, 111d * Math.Cos(latitude.Value * Math.PI / 180d));
            var minLat = latitude.Value - latitudeDelta; var maxLat = latitude.Value + latitudeDelta;
            var minLon = longitude.Value - longitudeDelta; var maxLon = longitude.Value + longitudeDelta;
            query = query.Where(x => x.Venue.Latitude >= minLat && x.Venue.Latitude <= maxLat &&
                x.Venue.Longitude >= minLon && x.Venue.Longitude <= maxLon);
        }

        var activities = await query.OrderBy(x => x.StartAt).Take(200).ToListAsync();
        var organizerIds = activities.Select(x => x.OrganizerId).Distinct().ToArray();
        var organizerNames = await db.PublicOrganizerProfiles.AsNoTracking().Where(x => organizerIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.DisplayName);
        var result = activities.Select(x => new
        {
            activity = ToPublicActivity(x, organizerNames.GetValueOrDefault(x.OrganizerId)),
            distanceKm = latitude.HasValue && longitude.HasValue
                ? Math.Round(Haversine(latitude.Value, longitude.Value, x.Venue.Latitude, x.Venue.Longitude), 1)
                : (double?)null
        }).Where(x => !x.distanceKm.HasValue || x.distanceKm <= effectiveRadius);
        if (availableOnly == true) result = result.Where(x => x.activity.AvailablePlaces > 0);
        result = sort?.Trim().ToLowerInvariant() switch
        {
            "distance" when latitude.HasValue && longitude.HasValue => result.OrderBy(x => x.distanceKm).ThenBy(x => x.activity.StartAt),
            "availability" => result.OrderByDescending(x => x.activity.AvailablePlaces).ThenBy(x => x.activity.StartAt),
            "price" => result.OrderBy(x => x.activity.Price).ThenBy(x => x.activity.StartAt),
            "date" => result.OrderBy(x => x.activity.StartAt),
            _ when latitude.HasValue && longitude.HasValue => result.OrderBy(x => x.distanceKm).ThenBy(x => x.activity.StartAt),
            _ => result.OrderBy(x => x.activity.StartAt)
        };
        return Results.Ok(new { total = result.Count(), items = result });
    }

    private static async Task<IResult> GeocodePublicLocationAsync(
        string? query, double? latitude, double? longitude, IHttpClientFactory httpClientFactory,
        IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var apiKey = configuration["YandexMaps:GeocoderApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return Results.Problem("Ключ API Геокодера Яндекса не настроен.", statusCode: StatusCodes.Status503ServiceUnavailable);
        if (string.IsNullOrWhiteSpace(query) && (!latitude.HasValue || !longitude.HasValue))
            return Results.ValidationProblem(Error("query", "Укажите адрес или координаты."));
        if (!string.IsNullOrWhiteSpace(query) && query.Trim().Length > 240)
            return Results.ValidationProblem(Error("query", "Адрес не должен превышать 240 символов."));
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return Results.ValidationProblem(Error("coordinates", "Координаты находятся вне допустимого диапазона."));

        var request = !string.IsNullOrWhiteSpace(query)
            ? query.Trim()
            : $"{longitude!.Value.ToString(CultureInfo.InvariantCulture)},{latitude!.Value.ToString(CultureInfo.InvariantCulture)}";
        var uri = $"https://geocode-maps.yandex.ru/v1/?apikey={Uri.EscapeDataString(apiKey)}&geocode={Uri.EscapeDataString(request)}&format=json&lang=ru_RU&results=1";
        using var response = await httpClientFactory.CreateClient("yandex-geocoder").GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Results.Problem("Геокодер Яндекса временно недоступен или ключ не имеет доступа к API Геокодера.", statusCode: StatusCodes.Status502BadGateway);

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        var featureMembers = json.RootElement.GetProperty("response").GetProperty("GeoObjectCollection").GetProperty("featureMember");
        if (featureMembers.GetArrayLength() == 0) return Results.NotFound(new { message = "Адрес не найден." });
        var geoObject = featureMembers[0].GetProperty("GeoObject");
        var position = geoObject.GetProperty("Point").GetProperty("pos").GetString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (position is not { Length: 2 } ||
            !double.TryParse(position[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var resultLongitude) ||
            !double.TryParse(position[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var resultLatitude))
            return Results.Problem("Геокодер вернул координаты в неизвестном формате.", statusCode: StatusCodes.Status502BadGateway);

        var metadata = geoObject.GetProperty("metaDataProperty").GetProperty("GeocoderMetaData");
        var addressNode = metadata.GetProperty("Address");
        var components = addressNode.GetProperty("Components").EnumerateArray()
            .Select(component => new { Kind = component.GetProperty("kind").GetString(), Name = component.GetProperty("name").GetString() ?? "" }).ToArray();
        string Component(string kind) => components.FirstOrDefault(component => component.Kind == kind)?.Name ?? "";
        var provinces = components.Where(component => component.Kind == "province").Select(component => component.Name).ToArray();
        return Results.Ok(new
        {
            coordinates = new[] { resultLatitude, resultLongitude },
            address = addressNode.GetProperty("formatted").GetString() ?? "",
            city = Component("locality") is { Length: > 0 } city ? city : provinces.LastOrDefault() ?? "",
            district = Component("district"),
            region = provinces.LastOrDefault() ?? ""
        });
    }

    private static async Task<IResult> CreatePublicVenueAsync(
        PublicVenueRequest request, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await IsAdultAsync(principal, userId, db)) return Results.Forbid();
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160) errors["name"] = ["Укажите название места до 160 символов."];
        if (string.IsNullOrWhiteSpace(request.City) || request.City.Trim().Length > 120) errors["city"] = ["Укажите город до 120 символов."];
        if (string.IsNullOrWhiteSpace(request.Address) || request.Address.Trim().Length > 240) errors["address"] = ["Укажите адрес или ориентир до 240 символов."];
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180) errors["coordinates"] = ["Поставьте метку в допустимой точке карты."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var venue = new SportsVenue
        {
            Slug = await UniqueVenueSlugAsync(db, request.Name, request.City), Name = request.Name.Trim(), City = request.City.Trim(),
            District = CleanPublicField(request.District), Address = request.Address.Trim(), Latitude = request.Latitude,
            Longitude = request.Longitude, Indoor = request.Indoor, Region = request.Region?.Trim() ?? string.Empty,
            IsVerified = false, Description = "Точка встречи добавлена организатором публичного события."
        };
        db.SportsVenues.Add(venue);
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_venue_created", nameof(SportsVenue), venue.Id.ToString());
        return Results.Created($"/api/public/venues/{Uri.EscapeDataString(venue.Slug)}", ToPublicVenue(venue));
    }

    private static async Task<IResult> GetMyPublicActivitiesAsync(
        ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var participations = await db.PublicActivityParticipants.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Activity.StartAt)
            .ToListAsync();
        var activityIds = participations.Select(x => x.PublicActivityId).ToArray();
        var activities = await db.PublicActivities.AsNoTracking()
            .Include(x => x.Sport).Include(x => x.Venue).Include(x => x.Participants)
            .Where(x => activityIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var organizerIds = activities.Values.Select(x => x.OrganizerId).Distinct().ToArray();
        var organizerNames = await db.PublicOrganizerProfiles.AsNoTracking()
            .Where(x => organizerIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.DisplayName);

        return Results.Ok(participations.Where(x => activities.ContainsKey(x.PublicActivityId)).Select(x =>
        {
            var activity = activities[x.PublicActivityId];
            return new ParticipantActivityDto(
                ToPublicActivity(activity, organizerNames.GetValueOrDefault(activity.OrganizerId), userId),
                ToParticipationDto(x));
        }));
    }

    private static async Task<IResult> GetPublicActivityParticipationAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var participation = await db.PublicActivityParticipants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PublicActivityId == id && x.UserId == userId);
        return participation is null ? Results.NotFound() : Results.Ok(ToParticipationDto(participation));
    }

    private static async Task<IResult> GetOrganizerParticipantsAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var activity = await db.PublicActivities.AsNoTracking().Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (activity is null) return Results.Forbid();
        var names = await ResolveParticipantNamesAsync(activity.Participants.Select(x => x.UserId), db);
        var items = activity.Participants.OrderBy(x => ParticipantStatusOrder(x.Status)).ThenBy(x => x.JoinedAt)
            .Select(x => new OrganizerParticipantDto(x.Id, x.UserId is not null ? names.GetValueOrDefault(x.UserId, "Участник") : x.GuestName ?? "Гость", x.GuestContact, x.Status.ToString(), x.JoinedAt, x.ConfirmedAt, x.CancelledAt));
        return Results.Ok(new OrganizerParticipantsDto(
            activity.Id,
            activity.Capacity,
            activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended),
            activity.Participants.Count(x => x.Status == PublicParticipantStatus.Waitlisted),
            activity.Participants.Count(x => x.Status is PublicParticipantStatus.Cancelled or PublicParticipantStatus.Rejected),
            items));
    }

    private static async Task<IResult> RemoveOrganizerParticipantAsync(
        int id, long participantId, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        var activity = await db.PublicActivities.Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (activity is null) return Results.Forbid();
        var participant = activity.Participants.SingleOrDefault(x => x.Id == participantId);
        if (participant is null) return Results.NotFound();
        if (participant.Status is PublicParticipantStatus.Cancelled or PublicParticipantStatus.Rejected)
            return Results.Conflict(new { message = "Участие уже отменено." });

        var releasedConfirmedPlace = participant.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended;
        participant.Status = PublicParticipantStatus.Rejected;
        participant.CancelledAt = DateTimeOffset.UtcNow;
        participant.ConfirmedAt = null;
        var promoted = releasedConfirmedPlace ? PromoteFirstWaitlisted(activity) : null;
        RefreshPublicActivityOccupancy(activity);
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_participant_removed", nameof(PublicActivity), id.ToString(), $"participant:{participant.Id}");
        if (promoted is not null)
            await audit.WriteAsync(promoted.UserId, "public_activity_waitlist_promoted", nameof(PublicActivity), id.ToString());
        if (transaction is not null) await transaction.CommitAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> JoinPublicActivityAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await IsAdultAsync(principal, userId, db))
            return Results.UnprocessableEntity(new { message = "Публичные активности доступны только совершеннолетним. Детский контур остаётся закрытым." });

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        var activity = await db.PublicActivities.Include(x => x.Participants).SingleOrDefaultAsync(x => x.Id == id);
        if (activity is null || activity.Visibility != PublicActivityVisibility.Public) return Results.NotFound();
        if (activity.OrganizerId == userId) return Results.Conflict(new { message = "Вы организатор этого события. Своё участие можно включить или выключить при редактировании события." });
        if (activity.Status is not (PublicActivityStatus.Published or PublicActivityStatus.Full) || activity.StartAt <= DateTimeOffset.UtcNow)
            return Results.Conflict(new { message = "Запись на это событие недоступна." });
        if (activity.RegistrationDeadline.HasValue && activity.RegistrationDeadline < DateTimeOffset.UtcNow)
            return Results.Conflict(new { message = "Срок регистрации завершён." });

        var existing = activity.Participants.SingleOrDefault(x => x.UserId == userId);
        if (existing is not null && existing.Status != PublicParticipantStatus.Cancelled)
            return Results.Conflict(new { message = "Вы уже записаны на это событие." });

        var confirmed = activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
        var waitlisted = activity.Participants.Count(x => x.Status == PublicParticipantStatus.Waitlisted);
        var status = confirmed < activity.Capacity ? PublicParticipantStatus.Confirmed : PublicParticipantStatus.Waitlisted;
        if (status == PublicParticipantStatus.Waitlisted && waitlisted >= activity.WaitlistCapacity)
            return Results.Conflict(new { message = "Свободных мест и мест в листе ожидания больше нет." });

        if (existing is null)
        {
            existing = new PublicActivityParticipant { PublicActivityId = id, UserId = userId };
            db.PublicActivityParticipants.Add(existing);
        }
        existing.Status = status;
        existing.JoinedAt = DateTimeOffset.UtcNow;
        existing.CancelledAt = null;
        existing.ConfirmedAt = status == PublicParticipantStatus.Confirmed ? DateTimeOffset.UtcNow : null;
        if (status == PublicParticipantStatus.Confirmed && confirmed + 1 >= activity.Capacity) activity.Status = PublicActivityStatus.Full;
        activity.Version++;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "Состояние записи изменилось. Обновите страницу и попробуйте снова." }); }
        await audit.WriteAsync(userId, "public_activity_joined", nameof(PublicActivity), id.ToString(), $"status:{status}");
        if (transaction is not null) await transaction.CommitAsync();
        return Results.Ok(new { activityId = id, status = status.ToString() });
    }

    private static async Task<IResult> JoinGuestPublicActivityAsync(
        int id, GuestJoinRequest request, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var name = request.Name?.Trim() ?? string.Empty;
        var contact = request.Contact?.Trim() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (name.Length is < 2 or > 80) errors["name"] = ["Укажите имя от 2 до 80 символов."];
        if (contact.Length is < 3 or > 120) errors["contact"] = ["Укажите телефон, email или Telegram для связи."];
        if (!request.AdultConfirmed) errors["adultConfirmed"] = ["Подтвердите, что вам исполнилось 18 лет."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        var activity = await db.PublicActivities.Include(x => x.Participants).SingleOrDefaultAsync(x => x.Id == id);
        if (activity is null || activity.Visibility != PublicActivityVisibility.Public) return Results.NotFound();
        if (activity.Status is not (PublicActivityStatus.Published or PublicActivityStatus.Full) || activity.StartAt <= DateTimeOffset.UtcNow)
            return Results.Conflict(new { message = "Запись на это событие недоступна." });
        if (activity.RegistrationDeadline.HasValue && activity.RegistrationDeadline < DateTimeOffset.UtcNow)
            return Results.Conflict(new { message = "Срок регистрации завершён." });

        var contactKey = Regex.Replace(contact.ToLowerInvariant(), "\\s+", string.Empty);
        var contactHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contactKey)));
        var existing = activity.Participants.SingleOrDefault(x => x.GuestContactHash == contactHash);
        if (existing is not null && existing.Status != PublicParticipantStatus.Cancelled)
            return Results.Conflict(new { message = "С этим контактом уже отметились на событии." });

        var confirmed = activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
        if (confirmed >= activity.Capacity)
            return Results.Conflict(new { message = "Свободных мест больше нет." });

        var cancellationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var participant = existing ?? new PublicActivityParticipant { GuestContactHash = contactHash };
        participant.GuestName = name;
        participant.GuestContact = contact;
        participant.GuestCancellationTokenHash = HashGuestCancellationToken(cancellationToken);
        participant.Status = PublicParticipantStatus.Confirmed;
        participant.JoinedAt = DateTimeOffset.UtcNow;
        participant.ConfirmedAt = DateTimeOffset.UtcNow;
        participant.CancelledAt = null;
        participant.Source = "guest-web";
        if (existing is null) activity.Participants.Add(participant);
        if (confirmed + 1 >= activity.Capacity) activity.Status = PublicActivityStatus.Full;
        activity.Version++;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "Состояние записи изменилось. Обновите страницу и попробуйте снова." }); }
        await audit.WriteAsync(null, "public_activity_guest_joined", nameof(PublicActivity), id.ToString(), "source:guest-web");
        if (transaction is not null) await transaction.CommitAsync();
        return Results.Ok(new
        {
            activityId = id,
            status = PublicParticipantStatus.Confirmed.ToString(),
            name,
            cancellationToken,
            managePath = $"/guest/participations/{cancellationToken}"
        });
    }

    private static async Task<IResult> GetGuestPublicParticipationAsync(
        string token, AppDbContext db, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var tokenHash = TryHashGuestCancellationToken(token);
        if (tokenHash is null) return Results.NotFound();
        var participant = await db.PublicActivityParticipants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.GuestCancellationTokenHash == tokenHash);
        if (participant is null) return Results.NotFound();
        var activity = await db.PublicActivities.AsNoTracking()
            .Include(x => x.Sport)
            .Include(x => x.Venue)
            .Include(x => x.Participants)
            .SingleAsync(x => x.Id == participant.PublicActivityId);
        var organizerName = await db.PublicOrganizerProfiles.AsNoTracking()
            .Where(x => x.UserId == activity.OrganizerId).Select(x => x.DisplayName).SingleOrDefaultAsync();
        return Results.Ok(new GuestParticipationDto(
            participant.GuestName ?? "Участник",
            participant.Status.ToString(),
            participant.JoinedAt,
            participant.CancelledAt,
            ToPublicActivity(activity, organizerName)));
    }

    private static async Task<IResult> CancelGuestPublicParticipationAsync(
        string token, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var tokenHash = TryHashGuestCancellationToken(token);
        if (tokenHash is null) return Results.NotFound();
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        var participant = await db.PublicActivityParticipants
            .Include(x => x.Activity).ThenInclude(x => x.Participants)
            .SingleOrDefaultAsync(x => x.GuestCancellationTokenHash == tokenHash);
        if (participant is null) return Results.NotFound();
        if (participant.Status == PublicParticipantStatus.Cancelled) return Results.NoContent();
        if (participant.Status is PublicParticipantStatus.Attended or PublicParticipantStatus.NoShow or PublicParticipantStatus.Rejected)
            return Results.Conflict(new { message = "Эту запись уже нельзя отменить." });
        var releasedConfirmedPlace = participant.Status == PublicParticipantStatus.Confirmed;
        participant.Status = PublicParticipantStatus.Cancelled;
        participant.CancelledAt = DateTimeOffset.UtcNow;
        participant.ConfirmedAt = null;
        var promoted = releasedConfirmedPlace ? PromoteFirstWaitlisted(participant.Activity) : null;
        RefreshPublicActivityOccupancy(participant.Activity);
        await db.SaveChangesAsync();
        await audit.WriteAsync(null, "public_activity_guest_left", nameof(PublicActivity), participant.PublicActivityId.ToString(), "source:guest-manage-link");
        if (promoted is not null)
            await audit.WriteAsync(promoted.UserId, "public_activity_waitlist_promoted", nameof(PublicActivity), participant.PublicActivityId.ToString());
        if (transaction is not null) await transaction.CommitAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> LeavePublicActivityAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        var activity = await db.PublicActivities.Include(x => x.Participants).SingleOrDefaultAsync(x => x.Id == id);
        if (activity is null) return Results.NotFound();
        var participant = activity.Participants.SingleOrDefault(x => x.UserId == userId && x.Status != PublicParticipantStatus.Cancelled);
        if (participant is null) return Results.NotFound();
        var releasedConfirmedPlace = participant.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended;
        participant.Status = PublicParticipantStatus.Cancelled;
        participant.CancelledAt = DateTimeOffset.UtcNow;
        participant.ConfirmedAt = null;
        var promoted = releasedConfirmedPlace ? PromoteFirstWaitlisted(activity) : null;
        RefreshPublicActivityOccupancy(activity);
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_left", nameof(PublicActivity), id.ToString());
        if (promoted is not null)
            await audit.WriteAsync(promoted.UserId, "public_activity_waitlist_promoted", nameof(PublicActivity), id.ToString());
        if (transaction is not null) await transaction.CommitAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> CreatePublicActivityAsync(
        PublicActivityRequest request, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await IsAdultAsync(principal, userId, db)) return Results.Forbid();
        var validation = ValidatePublicActivity(request);
        if (validation.Count > 0) return Results.ValidationProblem(validation);
        var sport = await db.Sports.SingleOrDefaultAsync(x => x.Id == request.SportId && x.IsActive && x.Slug != "futsal" && x.Slug != "mini-football");
        if (sport is null) return Results.ValidationProblem(Error("sportId", "Вид спорта недоступен."));
        var gameFormatError = ValidateGameFormat(sport.Slug, request.GameFormat);
        if (gameFormatError is not null) return Results.ValidationProblem(Error("gameFormat", gameFormatError));
        if (!await db.SportsVenues.AnyAsync(x => x.Id == request.VenueId && x.IsActive)) return Results.ValidationProblem(Error("venueId", "Площадка недоступна."));
        var item = new PublicActivity
        {
            Slug = await UniqueActivitySlugAsync(db, request.Title), SportId = request.SportId, EventType = request.EventType, GameFormat = CleanPublicField(request.GameFormat),
            Title = request.Title.Trim(), Description = request.Description.Trim(), OrganizerId = userId, SportsVenueId = request.VenueId,
            StartAt = request.StartAt, EndAt = request.EndAt, Capacity = request.Capacity, WaitlistCapacity = request.WaitlistCapacity,
            Price = request.Price, SkillLevel = request.SkillLevel.Trim(), MinimumAge = Math.Max(18, request.MinimumAge),
            MaximumAge = request.MaximumAge, EquipmentRequirements = CleanPublicField(request.EquipmentRequirements), Rules = CleanPublicField(request.Rules),
            CancellationPolicy = CleanPublicField(request.CancellationPolicy), RegistrationDeadline = request.RegistrationDeadline,
            IsRecurring = request.IsRecurring, RecurrenceRule = CleanPublicField(request.RecurrenceRule)
        };
        if (request.OrganizerParticipates)
        {
            item.Participants.Add(new PublicActivityParticipant
            {
                UserId = userId,
                Status = PublicParticipantStatus.Confirmed,
                JoinedAt = DateTimeOffset.UtcNow,
                ConfirmedAt = DateTimeOffset.UtcNow,
                Source = "organizer"
            });
        }
        db.PublicActivities.Add(item);
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_created", nameof(PublicActivity), item.Id.ToString());
        return Results.Created($"/api/public/activities/{Uri.EscapeDataString(item.Slug)}", new { item.Id, item.Slug });
    }

    private static async Task<IResult> UpdatePublicActivityAsync(
        int id, PublicActivityRequest request, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.PublicActivities.Include(x => x.Participants).SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (item is null) return Results.Forbid();
        if (item.Status is PublicActivityStatus.Completed or PublicActivityStatus.Archived) return Results.Conflict(new { message = "Завершённое событие нельзя редактировать." });
        var validation = ValidatePublicActivity(request);
        if (validation.Count > 0) return Results.ValidationProblem(validation);
        var sport = await db.Sports.SingleOrDefaultAsync(x => x.Id == request.SportId && x.IsActive && x.Slug != "futsal" && x.Slug != "mini-football");
        if (sport is null) return Results.ValidationProblem(Error("sportId", "Вид спорта недоступен."));
        var gameFormatError = ValidateGameFormat(sport.Slug, request.GameFormat);
        if (gameFormatError is not null) return Results.ValidationProblem(Error("gameFormat", gameFormatError));
        if (!await db.SportsVenues.AnyAsync(x => x.Id == request.VenueId && x.IsActive)) return Results.ValidationProblem(Error("venueId", "Площадка недоступна."));
        var organizerParticipation = item.Participants.SingleOrDefault(x => x.UserId == userId);
        var confirmedWithoutOrganizer = item.Participants.Count(x => x.UserId != userId &&
            x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
        var requestedConfirmed = confirmedWithoutOrganizer + (request.OrganizerParticipates ? 1 : 0);
        if (request.Capacity < requestedConfirmed) return Results.ValidationProblem(Error("capacity", "Вместимость не может быть меньше числа уже подтверждённых участников."));
        if (request.OrganizerParticipates)
        {
            if (organizerParticipation is null)
            {
                item.Participants.Add(new PublicActivityParticipant
                {
                    UserId = userId,
                    Status = PublicParticipantStatus.Confirmed,
                    JoinedAt = DateTimeOffset.UtcNow,
                    ConfirmedAt = DateTimeOffset.UtcNow,
                    Source = "organizer"
                });
            }
            else if (organizerParticipation.Status is PublicParticipantStatus.Cancelled or PublicParticipantStatus.Rejected)
            {
                organizerParticipation.Status = PublicParticipantStatus.Confirmed;
                organizerParticipation.JoinedAt = DateTimeOffset.UtcNow;
                organizerParticipation.ConfirmedAt = DateTimeOffset.UtcNow;
                organizerParticipation.CancelledAt = null;
            }
        }
        else if (organizerParticipation?.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended)
        {
            organizerParticipation.Status = PublicParticipantStatus.Cancelled;
            organizerParticipation.CancelledAt = DateTimeOffset.UtcNow;
            organizerParticipation.ConfirmedAt = null;
            PromoteFirstWaitlisted(item);
        }
        item.SportId = request.SportId; item.SportsVenueId = request.VenueId; item.EventType = request.EventType; item.GameFormat = CleanPublicField(request.GameFormat);
        item.Title = request.Title.Trim(); item.Description = request.Description.Trim(); item.StartAt = request.StartAt; item.EndAt = request.EndAt;
        item.Capacity = request.Capacity; item.WaitlistCapacity = request.WaitlistCapacity; item.Price = request.Price;
        item.SkillLevel = request.SkillLevel.Trim(); item.MinimumAge = Math.Max(18, request.MinimumAge); item.MaximumAge = request.MaximumAge;
        item.EquipmentRequirements = CleanPublicField(request.EquipmentRequirements); item.Rules = CleanPublicField(request.Rules); item.CancellationPolicy = CleanPublicField(request.CancellationPolicy);
        item.RegistrationDeadline = request.RegistrationDeadline; item.IsRecurring = request.IsRecurring; item.RecurrenceRule = CleanPublicField(request.RecurrenceRule);
        if (item.Status is PublicActivityStatus.Published or PublicActivityStatus.Full)
        {
            var confirmed = item.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
            item.Status = confirmed >= request.Capacity ? PublicActivityStatus.Full : PublicActivityStatus.Published;
        }
        item.Version++; item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_updated", nameof(PublicActivity), id.ToString());
        return Results.NoContent();
    }

    private static async Task<IResult> PublishPublicActivityAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.PublicActivities.SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (item is null) return Results.Forbid();
        if (item.StartAt <= DateTimeOffset.UtcNow || item.EndAt <= item.StartAt) return Results.Conflict(new { message = "Проверьте дату и время события." });
        item.Status = PublicActivityStatus.Published; item.PublishedAt ??= DateTimeOffset.UtcNow; item.Version++; item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_published", nameof(PublicActivity), id.ToString());
        return Results.NoContent();
    }

    private static async Task<IResult> CancelPublicActivityAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration,
        ITransactionalEmailSender emailSender, ILoggerFactory loggerFactory)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.PublicActivities.Include(x => x.Participants).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (item is null) return Results.Forbid();
        if (item.Status == PublicActivityStatus.Completed) return Results.Conflict(new { message = "Завершённое событие нельзя отменить." });
        var recipients = item.Participants
            .Where(x => x.UserId != userId && x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Waitlisted)
            .Select(x => x.User?.Email ?? (x.GuestContact?.Contains('@') == true ? x.GuestContact : null))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();
        item.Status = PublicActivityStatus.Cancelled; item.Version++; item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_cancelled", nameof(PublicActivity), id.ToString());

        var whenText = item.StartAt.ToString("d MMMM, HH:mm", new CultureInfo("ru-RU"));
        var (subject, html, text) = EmailTemplates.PublicActivityCancelled(item.Title, whenText, BuildUrl(configuration, "/sports"));
        foreach (var recipient in recipients) await TrySendAsync(emailSender, loggerFactory, recipient!, subject, html, text);

        return Results.NoContent();
    }

    private static async Task<IResult> DeletePublicActivityAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.PublicActivities.SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (item is null) return Results.Forbid();
        if (item.Status is PublicActivityStatus.Published or PublicActivityStatus.Full)
            return Results.Conflict(new { message = "Сначала отмените опубликованную активность, затем удалите её." });
        if (item.Status == PublicActivityStatus.Completed)
            return Results.Conflict(new { message = "Завершённая активность хранится в истории и не может быть удалена." });
        if (item.Status == PublicActivityStatus.Archived) return Results.NoContent();

        item.Status = PublicActivityStatus.Archived;
        item.Version++;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_deleted", nameof(PublicActivity), id.ToString());
        return Results.NoContent();
    }

    private static PublicActivityDto ToPublicActivity(PublicActivity activity, string? organizerName = null, string? currentUserId = null)
    {
        var confirmed = activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
        var waitlisted = activity.Participants.Count(x => x.Status == PublicParticipantStatus.Waitlisted);
        return new PublicActivityDto(activity.Id, activity.Slug, activity.Sport.Slug, activity.Sport.Name, activity.EventType.ToString(), activity.GameFormat,
            activity.Title, activity.Description, organizerName ?? "Организатор", activity.StartAt, activity.EndAt, activity.Price, activity.Currency, activity.SkillLevel,
            activity.MinimumAge, activity.MaximumAge, activity.Capacity, activity.WaitlistCapacity, confirmed, Math.Max(0, activity.Capacity - confirmed),
            Math.Max(0, activity.WaitlistCapacity - waitlisted),
            activity.Status.ToString(), activity.IsRecurring, activity.Participants.Any(x => x.UserId == activity.OrganizerId && x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended), activity.OrganizerId == currentUserId, activity.EquipmentRequirements, activity.Rules, activity.CancellationPolicy,
            new PublicVenueDto(activity.Venue.Id, activity.Venue.Slug, activity.Venue.Name, activity.Venue.City, activity.Venue.District,
                activity.Venue.Address, activity.Venue.Latitude, activity.Venue.Longitude, activity.Venue.Indoor, activity.Venue.IsVerified));
    }

    private static object ToPublicVenue(SportsVenue venue) => new
    {
        venue.Id, venue.Slug, venue.Name, venue.Description, venue.Country, venue.Region, venue.City,
        venue.District, venue.Address, venue.Latitude, venue.Longitude, venue.Indoor, venue.SurfaceType,
        venue.HasChangingRooms, venue.HasLighting, venue.HasParking, venue.Website, venue.IsVerified
    };

    private static Dictionary<string, string[]> ValidatePublicActivity(PublicActivityRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.IsDefined(request.EventType)) errors["eventType"] = ["Выберите допустимый формат события."];
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 120) errors["title"] = ["Укажите название до 120 символов."];
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 4000) errors["description"] = ["Добавьте описание до 4000 символов."];
        if (string.IsNullOrWhiteSpace(request.SkillLevel) || request.SkillLevel.Trim().Length > 80) errors["skillLevel"] = ["Укажите уровень участников до 80 символов."];
        if (request.StartAt <= DateTimeOffset.UtcNow) errors["startAt"] = ["Начало должно быть в будущем."];
        if (request.EndAt <= request.StartAt) errors["endAt"] = ["Окончание должно быть позже начала."];
        if (request.Capacity is < 2 or > 500) errors["capacity"] = ["Количество участников должно быть от 2 до 500."];
        if (request.WaitlistCapacity is < 0 or > 500) errors["waitlistCapacity"] = ["Некорректный размер листа ожидания."];
        if (request.Price < 0) errors["price"] = ["Цена не может быть отрицательной."];
        if (request.MaximumAge.HasValue && request.MaximumAge < Math.Max(18, request.MinimumAge)) errors["maximumAge"] = ["Максимальный возраст меньше минимального."];
        if (request.RegistrationDeadline.HasValue && request.RegistrationDeadline >= request.StartAt) errors["registrationDeadline"] = ["Регистрация должна закрываться до начала."];
        return errors;
    }

    private static string? ValidateGameFormat(string sportSlug, string? gameFormat)
    {
        if (!GameFormatsBySport.TryGetValue(sportSlug, out var allowedFormats))
            return string.IsNullOrWhiteSpace(gameFormat) ? null : "Для этого вида спорта формат игры не используется.";
        if (string.IsNullOrWhiteSpace(gameFormat)) return "Выберите формат игры.";
        return allowedFormats.Contains(gameFormat.Trim(), StringComparer.Ordinal) ? null : "Выбран недоступный формат игры.";
    }

    private static async Task<bool> IsAdultAsync(ClaimsPrincipal principal, string userId, AppDbContext db)
    {
        var birthDate = await db.Players.Where(x => x.UserId == userId).Select(x => (DateOnly?)x.DateOfBirth).SingleOrDefaultAsync();
        if (birthDate.HasValue) return birthDate.Value.AddYears(18) <= DateOnly.FromDateTime(DateTime.UtcNow);
        birthDate = await db.PublicOrganizerProfiles.Where(x => x.UserId == userId).Select(x => (DateOnly?)x.DateOfBirth).SingleOrDefaultAsync();
        if (birthDate.HasValue) return birthDate.Value.AddYears(18) <= DateOnly.FromDateTime(DateTime.UtcNow);
        return principal.IsInRole(Roles.Coach) || principal.IsInRole(Roles.Parent) || principal.IsInRole(Roles.SchoolOwner) ||
            principal.IsInRole(Roles.SchoolAdmin) || principal.IsInRole(Roles.Admin);
    }

    private static PublicActivityParticipant? PromoteFirstWaitlisted(PublicActivity activity)
    {
        var promoted = activity.Participants.Where(x => x.Status == PublicParticipantStatus.Waitlisted)
            .OrderBy(x => x.JoinedAt).FirstOrDefault();
        if (promoted is null) return null;
        promoted.Status = PublicParticipantStatus.Confirmed;
        promoted.ConfirmedAt = DateTimeOffset.UtcNow;
        return promoted;
    }

    private static void RefreshPublicActivityOccupancy(PublicActivity activity)
    {
        var confirmed = activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
        if (activity.Status is PublicActivityStatus.Published or PublicActivityStatus.Full)
            activity.Status = confirmed >= activity.Capacity ? PublicActivityStatus.Full : PublicActivityStatus.Published;
        activity.Version++;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static ParticipationDto ToParticipationDto(PublicActivityParticipant participant) =>
        new(participant.PublicActivityId, participant.Status.ToString(), participant.JoinedAt, participant.ConfirmedAt, participant.CancelledAt);

    private static int ParticipantStatusOrder(PublicParticipantStatus status) => status switch
    {
        PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended => 0,
        PublicParticipantStatus.Waitlisted => 1,
        _ => 2
    };

    private static async Task<Dictionary<string, string>> ResolveParticipantNamesAsync(IEnumerable<string?> userIds, AppDbContext db)
    {
        var ids = userIds.Where(x => x is not null).Select(x => x!).Distinct().ToArray();
        var names = await db.Players.AsNoTracking().Where(x => x.UserId != null && ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId!, x => (x.FirstName + " " + x.LastName).Trim());
        var coachNames = await db.CoachProfiles.AsNoTracking().Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.DisplayName);
        var organizerNames = await db.PublicOrganizerProfiles.AsNoTracking().Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.DisplayName);
        foreach (var pair in coachNames) names.TryAdd(pair.Key, pair.Value);
        foreach (var pair in organizerNames) names.TryAdd(pair.Key, pair.Value);
        return names;
    }

    private static async Task<string> UniqueActivitySlugAsync(AppDbContext db, string title)
    {
        var root = Regex.Replace(title.Trim().ToLowerInvariant(), "[^a-zа-яё0-9]+", "-").Trim('-');
        if (root.Length == 0) root = "activity";
        if (root.Length > 80) root = root[..80].TrimEnd('-');
        var slug = root;
        for (var suffix = 2; await db.PublicActivities.AnyAsync(x => x.Slug == slug); suffix++) slug = $"{root}-{suffix}";
        return slug;
    }

    private static async Task<string> UniqueVenueSlugAsync(AppDbContext db, string name, string city)
    {
        var root = Regex.Replace($"{name}-{city}".Trim().ToLowerInvariant(), "[^a-zа-яё0-9]+", "-").Trim('-');
        if (root.Length == 0) root = "venue";
        if (root.Length > 80) root = root[..80].TrimEnd('-');
        var slug = root;
        for (var suffix = 2; await db.SportsVenues.AnyAsync(x => x.Slug == slug); suffix++) slug = $"{root}-{suffix}";
        return slug;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0088;
        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLon = (lon2 - lon1) * Math.PI / 180d;
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) * Math.Pow(Math.Sin(dLon / 2), 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static bool PublicDiscoveryEnabled(IConfiguration configuration) => configuration.GetValue("PublicDiscovery:Enabled", false);
    private static string HashGuestCancellationToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static string? TryHashGuestCancellationToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized.Length == 64 && Regex.IsMatch(normalized, "^[a-f0-9]{64}$")
            ? HashGuestCancellationToken(normalized)
            : null;
    }
    private static string? CleanPublicField(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Dictionary<string, string[]> Error(string field, string message) => new() { [field] = [message] };

    public sealed record PublicVenueDto(int Id, string Slug, string Name, string City, string? District, string Address, double Latitude, double Longitude, bool Indoor, bool IsVerified);
    public sealed record PublicVenueRequest(string Name, string City, string? District, string Address, double Latitude, double Longitude, bool Indoor, string? Region);
    public sealed record PublicActivityDto(int Id, string Slug, string SportSlug, string Sport, string EventType, string? GameFormat, string Title, string Description, string OrganizerName,
        DateTimeOffset StartAt, DateTimeOffset EndAt, decimal Price, string Currency, string SkillLevel, int MinimumAge, int? MaximumAge,
        int Capacity, int WaitlistCapacity, int ParticipantsCount, int AvailablePlaces, int WaitlistAvailablePlaces, string Status, bool IsRecurring, bool OrganizerParticipates, bool IsCurrentUserOrganizer, string? EquipmentRequirements,
        string? Rules, string? CancellationPolicy, PublicVenueDto Venue);
    public sealed record PublicActivityRequest(int SportId, int VenueId, PublicActivityType EventType, string? GameFormat, string Title, string Description,
        DateTimeOffset StartAt, DateTimeOffset EndAt, int Capacity, int WaitlistCapacity, decimal Price, string SkillLevel,
        int MinimumAge, int? MaximumAge, string? EquipmentRequirements, string? Rules, string? CancellationPolicy,
        DateTimeOffset? RegistrationDeadline, bool IsRecurring, string? RecurrenceRule, bool OrganizerParticipates);
    public sealed record GuestJoinRequest(string? Name, string? Contact, bool AdultConfirmed);
    public sealed record GuestParticipationDto(string GuestName, string Status, DateTimeOffset JoinedAt, DateTimeOffset? CancelledAt, PublicActivityDto Activity);
    public sealed record ParticipationDto(int ActivityId, string Status, DateTimeOffset JoinedAt, DateTimeOffset? ConfirmedAt, DateTimeOffset? CancelledAt);
    public sealed record ParticipantActivityDto(PublicActivityDto Activity, ParticipationDto Participation);
    public sealed record OrganizerParticipantDto(long Id, string DisplayName, string? Contact, string Status, DateTimeOffset JoinedAt, DateTimeOffset? ConfirmedAt, DateTimeOffset? CancelledAt);
    public sealed record OrganizerParticipantsDto(int ActivityId, int Capacity, int ConfirmedCount, int WaitlistedCount, int CancelledCount, IEnumerable<OrganizerParticipantDto> Items);
}
