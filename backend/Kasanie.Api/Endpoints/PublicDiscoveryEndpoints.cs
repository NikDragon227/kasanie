using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Kasanie.Api.Application;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapPublicDiscovery(this IEndpointRouteBuilder app)
    {
        var publicApi = app.MapGroup("/api/public").RequireRateLimiting("public-discovery").WithTags("Sports Nearby — public catalog");

        publicApi.MapGet("/sports", async (AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            return Results.Ok(await db.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Slug, x.Name }).ToListAsync());
        });

        publicApi.MapGet("/activities", SearchPublicActivitiesAsync);

        publicApi.MapGet("/activities/{slug}", async (string slug, AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            var activity = await db.PublicActivities.AsNoTracking()
                .Include(x => x.Sport).Include(x => x.Venue).Include(x => x.Participants)
                .SingleOrDefaultAsync(x => x.Slug == slug && x.Visibility == PublicActivityVisibility.Public &&
                    (x.Status == PublicActivityStatus.Published || x.Status == PublicActivityStatus.Full));
            if (activity is null) return Results.NotFound();
            var organizerName = await db.PublicOrganizerProfiles.AsNoTracking().Where(x => x.UserId == activity.OrganizerId).Select(x => x.DisplayName).SingleOrDefaultAsync();
            return Results.Ok(ToPublicActivity(activity, organizerName));
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
        participantApi.MapPost("/{id:int}/join", JoinPublicActivityAsync);
        participantApi.MapPost("/{id:int}/leave", LeavePublicActivityAsync);

        var organizerApi = app.MapGroup("/api/organizer/activities").RequireAuthorization().RequireRateLimiting("public-action").WithTags("Sports Nearby — organizer");
        organizerApi.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var items = await db.PublicActivities.AsNoTracking().Include(x => x.Sport).Include(x => x.Venue).Include(x => x.Participants)
                .Where(x => x.OrganizerId == userId).OrderByDescending(x => x.StartAt).ToListAsync();
            var organizerName = await db.PublicOrganizerProfiles.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.DisplayName).SingleOrDefaultAsync();
            return Results.Ok(items.Select(x => ToPublicActivity(x, organizerName)));
        });
        organizerApi.MapPost("/", CreatePublicActivityAsync);
        organizerApi.MapPut("/{id:int}", UpdatePublicActivityAsync);
        organizerApi.MapPost("/{id:int}/publish", PublishPublicActivityAsync);
        organizerApi.MapPost("/{id:int}/cancel", CancelPublicActivityAsync);

        var organizerVenueApi = app.MapGroup("/api/organizer/venues").RequireAuthorization().RequireRateLimiting("public-action").WithTags("Sports Nearby — organizer venues");
        organizerVenueApi.MapPost("/", CreatePublicVenueAsync);
    }

    private static async Task<IResult> SearchPublicActivitiesAsync(
        string? sport, string? city, string? district, string? location, DateOnly? date, TimeOnly? time, PublicActivityType? type, bool? freeOnly,
        bool? availableOnly, double? latitude, double? longitude, double? radiusKm,
        AppDbContext db, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var query = db.PublicActivities.AsNoTracking().Include(x => x.Sport).Include(x => x.Venue).Include(x => x.Participants)
            .Where(x => x.Visibility == PublicActivityVisibility.Public &&
                (x.Status == PublicActivityStatus.Published || x.Status == PublicActivityStatus.Full) && x.EndAt > DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(sport)) query = query.Where(x => x.Sport.Slug == sport.Trim().ToLower());
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
        result = latitude.HasValue && longitude.HasValue ? result.OrderBy(x => x.distanceKm) : result.OrderBy(x => x.activity.StartAt);
        return Results.Ok(new { total = result.Count(), items = result });
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
        return Results.Created($"/api/public/venues/{venue.Slug}", ToPublicVenue(venue));
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
        if (activity.OrganizerId == userId) return Results.Conflict(new { message = "Организатор уже входит в событие и не занимает место участника." });
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
        var promoted = releasedConfirmedPlace
            ? activity.Participants.Where(x => x.Status == PublicParticipantStatus.Waitlisted)
                .OrderBy(x => x.JoinedAt).FirstOrDefault()
            : null;
        if (promoted is not null)
        {
            promoted.Status = PublicParticipantStatus.Confirmed;
            promoted.ConfirmedAt = DateTimeOffset.UtcNow;
        }
        var confirmed = activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
        if (activity.Status is PublicActivityStatus.Published or PublicActivityStatus.Full)
            activity.Status = confirmed >= activity.Capacity ? PublicActivityStatus.Full : PublicActivityStatus.Published;
        activity.Version++;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
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
        if (!await db.Sports.AnyAsync(x => x.Id == request.SportId && x.IsActive)) return Results.ValidationProblem(Error("sportId", "Вид спорта недоступен."));
        if (!await db.SportsVenues.AnyAsync(x => x.Id == request.VenueId && x.IsActive)) return Results.ValidationProblem(Error("venueId", "Площадка недоступна."));
        var item = new PublicActivity
        {
            Slug = await UniqueActivitySlugAsync(db, request.Title), SportId = request.SportId, EventType = request.EventType,
            Title = request.Title.Trim(), Description = request.Description.Trim(), OrganizerId = userId, SportsVenueId = request.VenueId,
            StartAt = request.StartAt, EndAt = request.EndAt, Capacity = request.Capacity, WaitlistCapacity = request.WaitlistCapacity,
            Price = request.Price, SkillLevel = request.SkillLevel.Trim(), MinimumAge = Math.Max(18, request.MinimumAge),
            MaximumAge = request.MaximumAge, EquipmentRequirements = CleanPublicField(request.EquipmentRequirements), Rules = CleanPublicField(request.Rules),
            CancellationPolicy = CleanPublicField(request.CancellationPolicy), RegistrationDeadline = request.RegistrationDeadline,
            IsRecurring = request.IsRecurring, RecurrenceRule = CleanPublicField(request.RecurrenceRule)
        };
        db.PublicActivities.Add(item);
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_created", nameof(PublicActivity), item.Id.ToString());
        return Results.Created($"/api/public/activities/{item.Slug}", new { item.Id, item.Slug });
    }

    private static async Task<IResult> UpdatePublicActivityAsync(
        int id, PublicActivityRequest request, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.PublicActivities.SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (item is null) return Results.Forbid();
        if (item.Status is PublicActivityStatus.Completed or PublicActivityStatus.Archived) return Results.Conflict(new { message = "Завершённое событие нельзя редактировать." });
        var validation = ValidatePublicActivity(request);
        if (validation.Count > 0) return Results.ValidationProblem(validation);
        if (!await db.Sports.AnyAsync(x => x.Id == request.SportId && x.IsActive)) return Results.ValidationProblem(Error("sportId", "Вид спорта недоступен."));
        if (!await db.SportsVenues.AnyAsync(x => x.Id == request.VenueId && x.IsActive)) return Results.ValidationProblem(Error("venueId", "Площадка недоступна."));
        var confirmed = await db.PublicActivityParticipants.CountAsync(x => x.PublicActivityId == id &&
            (x.Status == PublicParticipantStatus.Confirmed || x.Status == PublicParticipantStatus.Attended));
        if (request.Capacity < confirmed) return Results.ValidationProblem(Error("capacity", "Вместимость не может быть меньше числа уже подтверждённых участников."));
        item.SportId = request.SportId; item.SportsVenueId = request.VenueId; item.EventType = request.EventType;
        item.Title = request.Title.Trim(); item.Description = request.Description.Trim(); item.StartAt = request.StartAt; item.EndAt = request.EndAt;
        item.Capacity = request.Capacity; item.WaitlistCapacity = request.WaitlistCapacity; item.Price = request.Price;
        item.SkillLevel = request.SkillLevel.Trim(); item.MinimumAge = Math.Max(18, request.MinimumAge); item.MaximumAge = request.MaximumAge;
        item.EquipmentRequirements = CleanPublicField(request.EquipmentRequirements); item.Rules = CleanPublicField(request.Rules); item.CancellationPolicy = CleanPublicField(request.CancellationPolicy);
        item.RegistrationDeadline = request.RegistrationDeadline; item.IsRecurring = request.IsRecurring; item.RecurrenceRule = CleanPublicField(request.RecurrenceRule);
        if (item.Status is PublicActivityStatus.Published or PublicActivityStatus.Full)
            item.Status = confirmed >= request.Capacity ? PublicActivityStatus.Full : PublicActivityStatus.Published;
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
        int id, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
    {
        if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.PublicActivities.SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
        if (item is null) return Results.Forbid();
        if (item.Status == PublicActivityStatus.Completed) return Results.Conflict(new { message = "Завершённое событие нельзя отменить." });
        item.Status = PublicActivityStatus.Cancelled; item.Version++; item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await audit.WriteAsync(userId, "public_activity_cancelled", nameof(PublicActivity), id.ToString());
        return Results.NoContent();
    }

    private static PublicActivityDto ToPublicActivity(PublicActivity activity, string? organizerName = null)
    {
        var confirmed = activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
        var waitlisted = activity.Participants.Count(x => x.Status == PublicParticipantStatus.Waitlisted);
        return new PublicActivityDto(activity.Id, activity.Slug, activity.Sport.Slug, activity.Sport.Name, activity.EventType.ToString(),
            activity.Title, activity.Description, organizerName ?? "Организатор", activity.StartAt, activity.EndAt, activity.Price, activity.Currency, activity.SkillLevel,
            activity.MinimumAge, activity.MaximumAge, activity.Capacity, confirmed, Math.Max(0, activity.Capacity - confirmed),
            Math.Max(0, activity.WaitlistCapacity - waitlisted),
            activity.Status.ToString(), activity.IsRecurring, activity.EquipmentRequirements, activity.Rules, activity.CancellationPolicy,
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

    private static async Task<bool> IsAdultAsync(ClaimsPrincipal principal, string userId, AppDbContext db)
    {
        var birthDate = await db.Players.Where(x => x.UserId == userId).Select(x => (DateOnly?)x.DateOfBirth).SingleOrDefaultAsync();
        if (birthDate.HasValue) return birthDate.Value.AddYears(18) <= DateOnly.FromDateTime(DateTime.UtcNow);
        birthDate = await db.PublicOrganizerProfiles.Where(x => x.UserId == userId).Select(x => (DateOnly?)x.DateOfBirth).SingleOrDefaultAsync();
        if (birthDate.HasValue) return birthDate.Value.AddYears(18) <= DateOnly.FromDateTime(DateTime.UtcNow);
        return principal.IsInRole(Roles.Coach) || principal.IsInRole(Roles.Parent) || principal.IsInRole(Roles.SchoolOwner) ||
            principal.IsInRole(Roles.SchoolAdmin) || principal.IsInRole(Roles.RegionalAnalyst) || principal.IsInRole(Roles.Admin);
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
    private static string? CleanPublicField(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Dictionary<string, string[]> Error(string field, string message) => new() { [field] = [message] };

    public sealed record PublicVenueDto(int Id, string Slug, string Name, string City, string? District, string Address, double Latitude, double Longitude, bool Indoor, bool IsVerified);
    public sealed record PublicVenueRequest(string Name, string City, string? District, string Address, double Latitude, double Longitude, bool Indoor, string? Region);
    public sealed record PublicActivityDto(int Id, string Slug, string SportSlug, string Sport, string EventType, string Title, string Description, string OrganizerName,
        DateTimeOffset StartAt, DateTimeOffset EndAt, decimal Price, string Currency, string SkillLevel, int MinimumAge, int? MaximumAge,
        int Capacity, int ParticipantsCount, int AvailablePlaces, int WaitlistAvailablePlaces, string Status, bool IsRecurring, string? EquipmentRequirements,
        string? Rules, string? CancellationPolicy, PublicVenueDto Venue);
    public sealed record PublicActivityRequest(int SportId, int VenueId, PublicActivityType EventType, string Title, string Description,
        DateTimeOffset StartAt, DateTimeOffset EndAt, int Capacity, int WaitlistCapacity, decimal Price, string SkillLevel,
        int MinimumAge, int? MaximumAge, string? EquipmentRequirements, string? Rules, string? CancellationPolicy,
        DateTimeOffset? RegistrationDeadline, bool IsRecurring, string? RecurrenceRule);
}
