using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
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

        auth.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> users, AppDbContext db, IAuditService audit, ITransactionalEmailSender emailSender, IConfiguration configuration) =>
        {
            var errors = Validation.Register(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (!AgePolicy.CanRegisterIndependently(request.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)))
                return Results.UnprocessableEntity(new { code = "parent_required", message = "Игроку младше 14 лет профиль создаёт родитель в своём кабинете." });
            var municipality = await ResolveCityAsync(db, request.City);
            if (municipality is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["city"] = ["Выберите город из подсказок."] });

            var user = new ApplicationUser { Email = request.Email.Trim(), UserName = request.Email.Trim(), EmailConfirmed = false };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = result.Errors.Select(x => x.Description).ToArray() });
            await users.AddToRoleAsync(user, Roles.Player);
            db.Players.Add(new PlayerProfile
            {
                UserId = user.Id, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DateOfBirth = request.DateOfBirth,
                MunicipalityId = municipality.Id, PreferredPosition = request.PreferredPosition, DominantFoot = request.DominantFoot,
                ExperienceLevel = request.ExperienceLevel
            });
            await db.SaveChangesAsync();
            await audit.WriteAsync(user.Id, "registration", nameof(ApplicationUser), user.Id);
            await SendConfirmationAsync(user, users, emailSender, configuration);
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

        auth.MapPost("/resend-confirmation", async (EmailRequest request, UserManager<ApplicationUser> users, ITransactionalEmailSender emailSender, IConfiguration configuration) =>
        {
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is not null && !user.EmailConfirmed) await SendConfirmationAsync(user, users, emailSender, configuration);
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

        auth.MapPost("/forgot-password", async (EmailRequest request, UserManager<ApplicationUser> users, ITransactionalEmailSender emailSender, IConfiguration configuration) =>
        {
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is not null && user.EmailConfirmed)
            {
                var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(user));
                var url = BuildUrl(configuration, $"/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}");
                await emailSender.SendAsync(user.Email!, "Восстановление пароля — Касание", $"Откройте ссылку, чтобы задать новый пароль:\n{url}");
            }
            return Results.Ok(new { message = "Если такой подтверждённый аккаунт существует, письмо отправлено." });
        }).RequireRateLimiting("login");

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            if (request.NewPassword.Length < 10) return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["Пароль должен содержать не менее 10 символов."] });
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

        auth.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();

        app.MapGet("/api/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var roleList = await users.GetRolesAsync(user);
            return Results.Ok(new UserDto(user.Id, user.Email!, roleList.ToArray()));
        }).RequireAuthorization().WithTags("Authentication");

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

    private static async Task SendConfirmationAsync(ApplicationUser user, UserManager<ApplicationUser> users, ITransactionalEmailSender emailSender, IConfiguration configuration)
    {
        var token = EncodeToken(await users.GenerateEmailConfirmationTokenAsync(user));
        var url = BuildUrl(configuration, $"/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}");
        await emailSender.SendAsync(user.Email!, "Подтвердите email — Касание", $"Откройте ссылку, чтобы подтвердить email:\n{url}");
    }

    private static string BuildUrl(IConfiguration configuration, string path) => $"{configuration["App:PublicUrl"]?.TrimEnd('/') ?? "http://localhost"}{path}";
    private static string EncodeToken(string value) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));
    private static string? TryDecodeToken(string value)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)); }
        catch (FormatException) { return null; }
    }
}
