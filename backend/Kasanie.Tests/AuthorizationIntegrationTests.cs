using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kasanie.Tests;

public sealed class AuthorizationIntegrationTests
{
    [Fact]
    public async Task Coach_CannotOpenUnlinkedPlayerByDirectUrl()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.CoachProfiles.Add(new CoachProfile { Id = 1, UserId = "coach-a", DisplayName = "Coach A" });
            db.Players.AddRange(Player(1), Player(2));
            db.CoachPlayerLinks.Add(new CoachPlayerLink { CoachId = 1, PlayerId = 1, Status = LinkStatus.Active });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/coach/players/2", "coach-a", Roles.Coach));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Parent_CannotOpenUnlinkedChildByDirectUrl()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.ParentProfiles.Add(new ParentProfile { Id = 1, UserId = "parent-a" });
            db.Players.AddRange(Player(1), Player(2));
            db.ParentPlayerLinks.Add(new ParentPlayerLink { ParentId = 1, PlayerId = 1, Relationship = "Parent", IsPrimary = true, ConsentAccepted = true, ConsentVersion = "v1" });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/parent/children/2", "parent-a", Roles.Parent));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Player_CannotOpenAnotherPlayersTrainingSession()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var own = Player(1); own.UserId = "player-a";
            var other = Player(2); other.UserId = "player-b";
            var plan = new TrainingPlan { Id = 10, PlayerId = other.Id, WeekStart = new DateOnly(2026, 8, 17) };
            var day = new TrainingDay { Id = 20, TrainingPlan = plan, PlannedDate = new DateOnly(2026, 8, 18) };
            db.Players.AddRange(own, other);
            db.TrainingSessions.Add(new TrainingSession { Id = 30, PlayerId = other.Id, TrainingDay = day, Status = SessionStatus.InProgress });
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/training/sessions/30", "player-a", Roles.Player));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Analyst_SeesOnlyPlayersFromClaimedRegion()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var regionA = new Municipality { Id = 1, Name = "City A", Region = "Region A" };
            var regionB = new Municipality { Id = 2, Name = "City B", Region = "Region B" };
            db.Municipalities.AddRange(regionA, regionB);
            db.Players.AddRange(
                Player(1, regionA), Player(2, regionA), Player(3, regionA),
                Player(4, regionB), Player(5, regionB), Player(6, regionB));
            db.TrainingSessions.AddRange(
                CompletedSession(1, 4), CompletedSession(2, 5), CompletedSession(3, 6));
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/analytics/overview", "analyst-a", Roles.RegionalAnalyst, "Region A"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Region A", json.RootElement.GetProperty("region").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("totalActivePlayers").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("totalCompletedWorkouts").GetInt32());
        Assert.Equal("City A", json.RootElement.GetProperty("municipalities")[0].GetProperty("municipality").GetString());
    }

    [Fact]
    public async Task Analyst_WithoutRegionClaim_IsDenied()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/analytics/overview", "analyst-a", Roles.RegionalAnalyst));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Analyst_RegionBelowPrivacyThreshold_IsFullySuppressed()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var municipality = new Municipality { Id = 1, Name = "Small City", Region = "Small Region" };
            db.Municipalities.Add(municipality);
            db.Players.AddRange(Player(1, municipality), Player(2, municipality));
        });

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/analytics/overview", "analyst-a", Roles.RegionalAnalyst, "Small Region"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.GetProperty("suppressed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("totalActivePlayers").ValueKind);
        Assert.Empty(json.RootElement.GetProperty("municipalities").EnumerateArray());
    }

    [Fact]
    public async Task Admin_InviteLink_AllowsCoachToSetOwnPassword()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var inviteResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "new-coach@example.test", role = Roles.Coach, region = (string?)null }, "admin-a", Roles.Admin, csrf));
        var inviteBody = await inviteResponse.Content.ReadAsStringAsync();
        using var inviteJson = JsonDocument.Parse(inviteBody);

        Assert.True(inviteResponse.StatusCode == HttpStatusCode.Created, $"Expected 201, got {(int)inviteResponse.StatusCode}: {inviteBody}");
        var inviteUrl = new Uri(inviteJson.RootElement.GetProperty("inviteUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(inviteUrl.Query);
        Assert.Equal("https", inviteUrl.Scheme);
        Assert.Equal("new-coach@example.test", query["email"].ToString());

        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var resetResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Secure-Invite-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByEmailAsync("new-coach@example.test");
        Assert.NotNull(invited);
        Assert.True(invited.EmailConfirmed);
        Assert.True(await users.CheckPasswordAsync(invited, "Secure-Invite-2026!"));
        Assert.True(await users.IsInRoleAsync(invited, Roles.Coach));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.CoachProfiles.AnyAsync(x => x.UserId == invited.Id));
    }

    [Fact]
    public async Task ResetPassword_ReportsPasswordPolicyErrors_AndKeepsInviteUsable()
    {
        await using var factory = new TestApplicationFactory();
        using var adminClient = factory.CreateClient();
        var adminCsrf = await CsrfAsync(adminClient, "admin-a", Roles.Admin);
        using var inviteResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "password-policy@example.test", role = Roles.Coach, region = (string?)null }, "admin-a", Roles.Admin, adminCsrf));
        using var inviteJson = JsonDocument.Parse(await inviteResponse.Content.ReadAsStringAsync());
        var inviteUrl = new Uri(inviteJson.RootElement.GetProperty("inviteUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(inviteUrl.Query);

        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var weakPasswordResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "abcdefghij" }, null, null, inviteCsrf));
        var weakPasswordBody = await weakPasswordResponse.Content.ReadAsStringAsync();
        using var weakPasswordJson = JsonDocument.Parse(weakPasswordBody);

        Assert.True(weakPasswordResponse.StatusCode == HttpStatusCode.BadRequest, $"Expected 400, got {(int)weakPasswordResponse.StatusCode}: {weakPasswordBody}");
        Assert.True(weakPasswordJson.RootElement.GetProperty("errors").TryGetProperty("newPassword", out var passwordErrors));
        Assert.NotEmpty(passwordErrors.EnumerateArray());
        Assert.False(weakPasswordJson.RootElement.TryGetProperty("message", out _));

        using var validPasswordResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Kasanie-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, validPasswordResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByEmailAsync("password-policy@example.test");
        Assert.NotNull(invited);
        Assert.True(await users.CheckPasswordAsync(invited, "Kasanie-2026!"));
    }

    [Fact]
    public async Task Admin_BlocksInvitedUserWithoutDeletingAccount()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);
        using var inviteResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "new-parent@example.test", role = Roles.Parent, region = (string?)null }, "admin-a", Roles.Admin, csrf));
        var inviteBody = await inviteResponse.Content.ReadAsStringAsync();
        Assert.True(inviteResponse.StatusCode == HttpStatusCode.Created, $"Expected 201, got {(int)inviteResponse.StatusCode}: {inviteBody}");
        using var inviteJson = JsonDocument.Parse(inviteBody);
        var userId = inviteJson.RootElement.GetProperty("id").GetString()!;

        using var lockResponse = await client.SendAsync(JsonRequest(HttpMethod.Put, $"/api/admin/users/{userId}/lock", new { locked = true }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.NoContent, lockResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByIdAsync(userId);
        Assert.NotNull(invited);
        Assert.True(await users.IsLockedOutAsync(invited));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.ParentProfiles.AnyAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task Admin_InviteAnalyst_RequiresAndStoresRegionClaim()
    {
        await using var factory = new TestApplicationFactory();
        await factory.SeedAsync(db => db.Municipalities.Add(new Municipality { Name = "Kazan", Region = "Tatarstan" }));
        using var client = factory.CreateClient();
        var csrf = await CsrfAsync(client, "admin-a", Roles.Admin);

        using var missingRegion = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "analyst-one@example.test", role = Roles.RegionalAnalyst, region = (string?)null }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.BadRequest, missingRegion.StatusCode);

        using var inviteResponse = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "analyst-one@example.test", role = Roles.RegionalAnalyst, region = "Tatarstan" }, "admin-a", Roles.Admin, csrf));
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var invited = await users.FindByEmailAsync("analyst-one@example.test");
        Assert.NotNull(invited);
        var claims = await users.GetClaimsAsync(invited);
        Assert.Contains(claims, x => x.Type == KasanieClaimTypes.AnalyticsRegion && x.Value == "Tatarstan");
    }

    [Fact]
    public async Task Admin_ReissuesUnusedInvite_WithNewWorkingLink()
    {
        await using var factory = new TestApplicationFactory();
        using var adminClient = factory.CreateClient();
        var adminCsrf = await CsrfAsync(adminClient, "admin-a", Roles.Admin);
        using var createResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "reissue@example.test", role = Roles.Coach, region = (string?)null }, "admin-a", Roles.Admin, adminCsrf));
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var userId = createJson.RootElement.GetProperty("id").GetString()!;
        var originalUrl = createJson.RootElement.GetProperty("inviteUrl").GetString()!;

        using var reissueResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, $"/api/admin/users/{userId}/invite", new { }, "admin-a", Roles.Admin, adminCsrf));
        using var reissueJson = JsonDocument.Parse(await reissueResponse.Content.ReadAsStringAsync());
        var reissuedUrl = reissueJson.RootElement.GetProperty("inviteUrl").GetString()!;
        Assert.Equal(HttpStatusCode.OK, reissueResponse.StatusCode);
        Assert.NotEqual(originalUrl, reissuedUrl);

        var query = QueryHelpers.ParseQuery(new Uri(reissuedUrl).Query);
        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var resetResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Secure-Reissue-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotReissueInvite_AfterPasswordWasSet()
    {
        await using var factory = new TestApplicationFactory();
        using var adminClient = factory.CreateClient();
        var adminCsrf = await CsrfAsync(adminClient, "admin-a", Roles.Admin);
        using var createResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/admin/users", new { email = "activated@example.test", role = Roles.Parent, region = (string?)null }, "admin-a", Roles.Admin, adminCsrf));
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var userId = createJson.RootElement.GetProperty("id").GetString()!;
        var inviteUrl = new Uri(createJson.RootElement.GetProperty("inviteUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(inviteUrl.Query);

        using var inviteClient = factory.CreateClient();
        var inviteCsrf = await CsrfAsync(inviteClient);
        using var resetResponse = await inviteClient.SendAsync(JsonRequest(HttpMethod.Post, "/api/auth/reset-password", new { email = query["email"].ToString(), token = query["token"].ToString(), newPassword = "Secure-Activated-2026!" }, null, null, inviteCsrf));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using var reissueResponse = await adminClient.SendAsync(JsonRequest(HttpMethod.Post, $"/api/admin/users/{userId}/invite", new { }, "admin-a", Roles.Admin, adminCsrf));
        Assert.Equal(HttpStatusCode.Conflict, reissueResponse.StatusCode);
    }

    private static async Task<string> CsrfAsync(HttpClient client, string? userId = null, string? role = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        if (userId is not null) request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        if (role is not null) request.Headers.Add(TestAuthHandler.RoleHeader, role);
        using var response = await client.SendAsync(request);
        var cookie = response.Headers.GetValues("Set-Cookie").Select(x => x.Split(';', 2)[0]).Single(x => x.StartsWith("Kasanie.Antiforgery=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, object body, string? userId, string? role, string csrf)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (userId is not null) request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        if (role is not null) request.Headers.Add(TestAuthHandler.RoleHeader, role);
        return request;
    }

    private static HttpRequestMessage Get(string path, string userId, string role, string? region = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.RoleHeader, role);
        if (region is not null) request.Headers.Add(TestAuthHandler.RegionHeader, region);
        return request;
    }

    private static PlayerProfile Player(int id, Municipality? municipality = null) => new()
    {
        Id = id,
        FirstName = "Player",
        LastName = id.ToString(),
        DateOfBirth = new DateOnly(2010, 1, 1),
        MunicipalityId = municipality?.Id ?? 1,
        Municipality = municipality!,
        PreferredPosition = "Midfielder",
        DominantFoot = "Right",
        ExperienceLevel = "Amateur"
    };

    private static TrainingSession CompletedSession(int id, int playerId)
    {
        var plan = new TrainingPlan { Id = 100 + id, PlayerId = playerId, WeekStart = new DateOnly(2026, 8, 17) };
        var day = new TrainingDay { Id = 200 + id, TrainingPlan = plan, PlannedDate = new DateOnly(2026, 8, 18) };
        return new TrainingSession { Id = 300 + id, PlayerId = playerId, TrainingDay = day, Status = SessionStatus.Completed, CompletedAt = DateTimeOffset.UtcNow };
    }
}

internal sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"kasanie-auth-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Analytics:MinimumGroupSize", "3");
        builder.UseSetting("App:PublicUrl", "https://prokasanie.test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            var databaseServices = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            services.AddDbContext<AppDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .UseInternalServiceProvider(databaseServices));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }
}

internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-User";
    public const string RoleHeader = "X-Test-Role";
    public const string RegionHeader = "X-Test-Region";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || !Request.Headers.TryGetValue(RoleHeader, out var role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };
        if (Request.Headers.TryGetValue(RegionHeader, out var region))
            claims.Add(new Claim(KasanieClaimTypes.AnalyticsRegion, region.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
