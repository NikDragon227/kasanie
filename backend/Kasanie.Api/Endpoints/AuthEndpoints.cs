using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapAuth(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");
        auth.MapGet("/csrf", (HttpContext http, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { token = tokens.RequestToken });
        }).AllowAnonymous();

        auth.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> users, AppDbContext db, IAuditService audit, ITransactionalEmailSender emailSender, IConfiguration configuration, ILoggerFactory loggerFactory) =>
        {
            var errors = Validation.Register(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (!AgePolicy.CanRegisterIndependently(request.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)))
                return Results.UnprocessableEntity(new { code = "parent_required", message = "Игроку младше 14 лет профиль создаёт родитель в своём кабинете." });
            var user = new ApplicationUser { Email = request.Email.Trim(), UserName = request.Email.Trim(), EmailConfirmed = false };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = result.Errors.Select(x => x.Description).ToArray() });
            await users.AddToRoleAsync(user, Roles.Player);
            db.Players.Add(new PlayerProfile
            {
                UserId = user.Id, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DateOfBirth = request.DateOfBirth,
                PreferredPosition = "", DominantFoot = "", ExperienceLevel = ""
            });
            await db.SaveChangesAsync();
            await audit.WriteAsync(user.Id, "registration", nameof(ApplicationUser), user.Id);
            await SendConfirmationAsync(user, users, emailSender, configuration, loggerFactory);
            return Results.Created("/api/me", new { message = "Аккаунт создан. Подтвердите email по ссылке из письма." });
        }).RequireRateLimiting("login");

        auth.MapPost("/register-organizer", async (RegisterOrganizerRequest request, UserManager<ApplicationUser> users, AppDbContext db, IAuditService audit, ITransactionalEmailSender emailSender, IConfiguration configuration, ILoggerFactory loggerFactory) =>
        {
            if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
            var errors = Validation.RegisterOrganizer(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (AgePolicy.GetAge(request.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)) < 18)
                return Results.UnprocessableEntity(new { code = "adult_required", message = "Создавать публичные активности могут только совершеннолетние." });
            var municipality = await ResolveCityAsync(db, request.City);
            if (municipality is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["city"] = ["Выберите город из подсказок."] });

            var normalizedEmail = request.Email.Trim();
            var user = new ApplicationUser { Email = normalizedEmail, UserName = normalizedEmail, EmailConfirmed = false };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = result.Errors.Select(x => x.Description).ToArray() });
            var roleResult = await users.AddToRoleAsync(user, Roles.Organizer);
            if (!roleResult.Succeeded)
            {
                await users.DeleteAsync(user);
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = roleResult.Errors.Select(x => x.Description).ToArray() });
            }
            db.PublicOrganizerProfiles.Add(new PublicOrganizerProfile
            {
                UserId = user.Id,
                DisplayName = request.DisplayName.Trim(),
                DateOfBirth = request.DateOfBirth,
                MunicipalityId = municipality.Id
            });
            await db.SaveChangesAsync();
            await audit.WriteAsync(user.Id, "organizer_registration", nameof(ApplicationUser), user.Id);
            await SendConfirmationAsync(user, users, emailSender, configuration, loggerFactory);
            return Results.Created("/api/me", new { message = "Аккаунт организатора создан. Подтвердите email по ссылке из письма." });
        }).RequireRateLimiting("login");

        auth.MapPost("/register-portal-user", async (RegisterPortalUserRequest request, UserManager<ApplicationUser> users, AppDbContext db, IAuditService audit, ITransactionalEmailSender emailSender, IConfiguration configuration, ILoggerFactory loggerFactory) =>
        {
            var errors = Validation.RegisterPortalUser(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (AgePolicy.GetAge(request.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)) < 18)
                return Results.UnprocessableEntity(new { code = "adult_required", message = "Самостоятельная регистрация родителя или тренера доступна только с 18 лет." });

            var normalizedEmail = request.Email.Trim();
            var user = new ApplicationUser { Email = normalizedEmail, UserName = normalizedEmail, EmailConfirmed = false };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = result.Errors.Select(x => x.Description).ToArray() });
            var roleResult = await users.AddToRoleAsync(user, request.Role);
            if (!roleResult.Succeeded)
            {
                await users.DeleteAsync(user);
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = roleResult.Errors.Select(x => x.Description).ToArray() });
            }

            if (request.Role == Roles.Coach)
                db.CoachProfiles.Add(new CoachProfile { UserId = user.Id, DisplayName = request.DisplayName.Trim() });
            else
                db.ParentProfiles.Add(new ParentProfile { UserId = user.Id });

            await db.SaveChangesAsync();
            await audit.WriteAsync(user.Id, request.Role == Roles.Coach ? "coach_registration" : "parent_registration", nameof(ApplicationUser), user.Id);
            await SendConfirmationAsync(user, users, emailSender, configuration, loggerFactory);
            return Results.Created("/api/me", new { message = "Аккаунт создан. Подтвердите email по ссылке из письма." });
        }).RequireRateLimiting("login");

        auth.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.Unauthorized();
            if (!user.EmailConfirmed) return Results.Problem("Подтвердите email перед входом.", statusCode: StatusCodes.Status403Forbidden, extensions: new Dictionary<string, object?> { ["code"] = "email_not_confirmed" });
            var result = await signIn.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                db.AuditLogs.Add(new AuditLog { UserId = user.Id, EventType = result.IsLockedOut ? "login_locked" : "login_failed", EntityType = nameof(ApplicationUser), EntityId = user.Id });
                await db.SaveChangesAsync();
                return result.IsLockedOut ? Results.Problem("Аккаунт временно заблокирован.", statusCode: 423) : Results.Unauthorized();
            }
            user.LastActiveAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = user.Id, EventType = "login", EntityType = nameof(ApplicationUser), EntityId = user.Id });
            await db.SaveChangesAsync();
            var roleList = await users.GetRolesAsync(user);
            return Results.Ok(new UserDto(user.Id, user.Email!, roleList.ToArray()));
        }).RequireRateLimiting("login");

        auth.MapPost("/resend-confirmation", async (EmailRequest request, UserManager<ApplicationUser> users, ITransactionalEmailSender emailSender, IConfiguration configuration, ILoggerFactory loggerFactory) =>
        {
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is not null && !user.EmailConfirmed) await SendConfirmationAsync(user, users, emailSender, configuration, loggerFactory);
            return Results.Ok(new { message = "Если аккаунт ожидает подтверждения, письмо отправлено." });
        }).RequireRateLimiting("login");

        auth.MapPost("/confirm-email", async (ConfirmEmailRequest request, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var user = await users.FindByIdAsync(request.UserId); if (user is null) return Results.BadRequest(new { message = "Ссылка недействительна или устарела." });
            var token = TryDecodeToken(request.Token); if (token is null) return Results.BadRequest(new { message = "Ссылка недействительна или устарела." });
            var result = await users.ConfirmEmailAsync(user, token);
            if (!result.Succeeded) return Results.BadRequest(new { message = "Ссылка недействительна или устарела." });
            db.AuditLogs.Add(new AuditLog { UserId = user.Id, EventType = "email_confirmed", EntityType = nameof(ApplicationUser), EntityId = user.Id }); await db.SaveChangesAsync();
            return Results.Ok(new { message = "Email подтверждён. Теперь можно войти." });
        }).RequireRateLimiting("login");

        auth.MapPost("/forgot-password", async (EmailRequest request, UserManager<ApplicationUser> users, ITransactionalEmailSender emailSender, IConfiguration configuration, ILoggerFactory loggerFactory) =>
        {
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is not null && user.EmailConfirmed)
            {
                var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(user));
                var url = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}");
                var (subject, html, text) = EmailTemplates.PasswordReset(url);
                await TrySendAsync(emailSender, loggerFactory, user.Email!, subject, html, text);
            }
            return Results.Ok(new { message = "Если такой подтверждённый аккаунт существует, письмо отправлено." });
        }).RequireRateLimiting("login");

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8) return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["Пароль должен содержать не менее 8 символов."] });
            var user = await users.FindByEmailAsync(request.Email.Trim()); if (user is null) return Results.BadRequest(new { message = "Ссылка недействительна или устарела." });
            var token = TryDecodeToken(request.Token); if (token is null) return Results.BadRequest(new { message = "Ссылка недействительна или устарела." });
            var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded)
            {
                var passwordErrors = result.Errors.Where(x => x.Code.StartsWith("Password")).Select(x => x.Description).ToArray();
                if (passwordErrors.Length > 0)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = passwordErrors });
                return Results.BadRequest(new { message = "Ссылка недействительна или устарела." });
            }
            db.AuditLogs.Add(new AuditLog { UserId = user.Id, EventType = "password_reset", EntityType = nameof(ApplicationUser), EntityId = user.Id }); await db.SaveChangesAsync();
            return Results.Ok(new { message = "Пароль обновлён. Теперь можно войти." });
        }).RequireRateLimiting("login");

        auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db) =>
        {
            if (string.IsNullOrEmpty(request.CurrentPassword))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["Введите текущий пароль."] });
            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["Пароль должен содержать не менее 8 символов."] });
            var user = await users.GetUserAsync(principal); if (user is null) return Results.Unauthorized();
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = new Dictionary<string, string[]>();
                var currentPasswordErrors = result.Errors.Where(x => x.Code == "PasswordMismatch").Select(x => x.Description).ToArray();
                var newPasswordErrors = result.Errors.Where(x => x.Code.StartsWith("Password") && x.Code != "PasswordMismatch").Select(x => x.Description).ToArray();
                if (currentPasswordErrors.Length > 0) errors["currentPassword"] = currentPasswordErrors;
                if (newPasswordErrors.Length > 0) errors["newPassword"] = newPasswordErrors;
                if (errors.Count > 0) return Results.ValidationProblem(errors);
                return Results.BadRequest(new { message = "Не удалось изменить пароль." });
            }
            await signIn.RefreshSignInAsync(user);
            db.AuditLogs.Add(new AuditLog { UserId = user.Id, EventType = "password_changed", EntityType = nameof(ApplicationUser), EntityId = user.Id }); await db.SaveChangesAsync();
            return Results.Ok(new { message = "Пароль изменён." });
        }).RequireAuthorization().RequireRateLimiting("login");

        auth.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();

        app.MapGet("/api/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            // Гость — это не ошибка: возвращаем 200 с телом null, чтобы фронт не сыпал 401 в консоль.
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Content("null", "application/json");
            var roleList = await users.GetRolesAsync(user);
            return Results.Ok(new UserDto(user.Id, user.Email!, roleList.ToArray()));
        }).AllowAnonymous().WithTags("Authentication");

        app.MapGet("/api/reference/cities", async (string? q, AppDbContext db) =>
        {
            var query = db.Municipalities.AsNoTracking().Where(x => x.IsActive);
            var normalized = NormalizeCity(q);
            if (normalized.Length > 0)
                query = query.Where(x => EF.Functions.ILike(x.Name, $"%{normalized}%"));

            var limit = normalized.Length > 0 ? 12 : 30;
            return Results.Ok(await query.OrderBy(x => x.Name).Take(limit)
                .Select(x => new { city = x.Name, x.Region }).ToListAsync());
        }).AllowAnonymous();
    }

    private static async Task SendConfirmationAsync(ApplicationUser user, UserManager<ApplicationUser> users, ITransactionalEmailSender emailSender, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var token = EncodeToken(await users.GenerateEmailConfirmationTokenAsync(user));
        var url = BuildUrl(configuration, $"/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}");
        var (subject, html, text) = EmailTemplates.ConfirmEmail(url);
        await TrySendAsync(emailSender, loggerFactory, user.Email!, subject, html, text);
    }

    // Письмо-подтверждение/сброс не должно ронять запрос: аккаунт уже создан, а
    // недоставку (например, адрес в списке подавления SMTP-провайдера) видно в трекере ошибок.
    // Пользователь запросит письмо повторно через /resend-confirmation или /forgot-password.
    private static async Task TrySendAsync(ITransactionalEmailSender emailSender, ILoggerFactory loggerFactory, string recipient, string subject, string html, string text)
    {
        try
        {
            await emailSender.SendAsync(recipient, subject, html, text);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Kasanie.Api.Auth.Email")
                .LogError(ex, "Не удалось отправить письмо на {Recipient}: {Subject}", recipient, subject);
        }
    }

    private static string BuildUrl(IConfiguration configuration, string path) => $"{configuration["App:PublicUrl"]?.TrimEnd('/') ?? "http://localhost"}{path}";
    private static string EncodeToken(string value) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));
    private static string? TryDecodeToken(string value)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)); }
        catch (FormatException) { return null; }
    }
}
