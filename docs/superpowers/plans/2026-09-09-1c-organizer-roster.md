# 1C — Организатор: состав участников и жалобы — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use `- [ ]`.

**Goal:** Give the organizer of a «Спорт рядом» activity a manual "add participant" action and a post-activity cross-organizer complaint mechanism surfaced as a `!` badge in the Participants tab.

**Architecture:** New `PublicActivityParticipantReport` entity (identity-keyed by `SubjectUserId` xor `SubjectGuestContactHash`), report endpoints in a new `PublicDiscoveryReportsEndpoints.cs` partial-class file, a badge count folded into `GetOrganizerParticipantsAsync`, and a manual-add endpoint that shares an extracted `ResolveParticipantPlacement` helper with `JoinGuestPublicActivityAsync`.

**Tech Stack:** .NET 10 Minimal API, EF Core + Npgsql (PostgreSQL), xUnit integration tests (InMemory provider), React 19 + TS + Vite + Vitest.

**Spec:** `docs/superpowers/specs/2026-09-09-1c-organizer-roster.md`

## Global Constraints

- All user-facing text in Russian; code identifiers English.
- Backend build/test only via `mcr.microsoft.com/dotnet/sdk:10.0` container (local SDK is 9). See memory `backend-build-requires-docker-sdk10`.
- Every FK is `DeleteBehavior.Restrict` (global `foreach` in `OnModelCreating`) — do not override.
- Enums persist as `int` (convention).
- Write handlers guard `db.Database.IsRelational()` before opening a `Serializable` transaction (InMemory tests).
- Multi-actor integration tests need one `factory.CreateClient()` per actor (CSRF helper only captures the antiforgery cookie on first call).
- `PublicActivityStatus.Completed` is never assigned — "activity happened" = `EndAt < UtcNow`.
- Run the full `Kasanie.Tests` suite (78 as of 1B) after each backend task; frontend `npm run lint && npx tsc --noEmit && npm run test` after each frontend task.

Container test command (run from `backend/`):

```bash
WINPATH=$(pwd -W)
MSYS_NO_PATHCONV=1 docker run --rm -v "${WINPATH}:/src" -v kasanie-nuget:/root/.nuget/packages -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -lc "dotnet test Kasanie.Tests/Kasanie.Tests.csproj --nologo -v minimal"
```

---

### Task 1: Extract `ResolveParticipantPlacement`, no behavior change

**Files:**
- Modify: `backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs` (`JoinGuestPublicActivityAsync` ~443-466; add helper near `PromoteFirstWaitlisted` ~800)
- Test: `backend/Kasanie.Tests/AuthorizationIntegrationTests.cs` (existing guest-join/waitlist tests are the guard)

**Interfaces:**
- Produces: `private readonly record struct ParticipantPlacement(PublicParticipantStatus Status, bool BecomesFull, string? RejectionMessage);` and `private static ParticipantPlacement ResolveParticipantPlacement(PublicActivity activity)`.

- [ ] **Step 1: Run the existing suite to record the green baseline**
  Run the container test command. Expected: `Passed! - Failed: 0, Passed: 78`.

- [ ] **Step 2: Add the helper** near `PromoteFirstWaitlisted`:

```csharp
private readonly record struct ParticipantPlacement(
    PublicParticipantStatus Status, bool BecomesFull, string? RejectionMessage);

/// <summary>Двухуровневое решение confirmed/waitlist/отказ для новой записи. Без побочных эффектов.</summary>
private static ParticipantPlacement ResolveParticipantPlacement(PublicActivity activity)
{
    var confirmed = activity.Participants.Count(x => x.Status is PublicParticipantStatus.Confirmed or PublicParticipantStatus.Attended);
    var waitlisted = activity.Participants.Count(x => x.Status == PublicParticipantStatus.Waitlisted);
    var status = confirmed < activity.Capacity ? PublicParticipantStatus.Confirmed : PublicParticipantStatus.Waitlisted;
    if (status == PublicParticipantStatus.Waitlisted && waitlisted >= activity.WaitlistCapacity)
        return new(default, false, "Свободных мест и мест в листе ожидания больше нет.");
    return new(status, status == PublicParticipantStatus.Confirmed && confirmed + 1 >= activity.Capacity, null);
}
```

- [ ] **Step 3: Rewrite the guest-join placement block** (lines ~449-466) to:

```csharp
var placement = ResolveParticipantPlacement(activity);
if (placement.RejectionMessage is not null) return Results.Conflict(new { message = placement.RejectionMessage });

var cancellationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
var participant = existing ?? new PublicActivityParticipant { GuestContactHash = contactHash };
participant.GuestName = name;
participant.GuestContact = contact;
participant.GuestCancellationTokenHash = HashGuestCancellationToken(cancellationToken);
participant.Status = placement.Status;
participant.JoinedAt = DateTimeOffset.UtcNow;
participant.ConfirmedAt = placement.Status == PublicParticipantStatus.Confirmed ? DateTimeOffset.UtcNow : null;
participant.CancelledAt = null;
participant.Source = "guest-web";
if (existing is null) activity.Participants.Add(participant);
if (placement.BecomesFull) activity.Status = PublicActivityStatus.Full;
```

(Keep the dedup check on `existing` and the `$"source:guest-web;status:{...}"` audit line unchanged — reference `placement.Status` there.)

- [ ] **Step 4: Run the suite**
  Run the container test command. Expected: `Passed: 78`, unchanged. Any guest-join / waitlist assertion change = regression, revert and retry.

- [ ] **Step 5: Commit** (only when the user authorizes committing)

```bash
git add backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs
git commit -m "refactor(sports): extract ResolveParticipantPlacement from guest join"
```

---

### Task 2: `AddOrganizerParticipantAsync` — manual add endpoint

**Files:**
- Modify: `backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs` (map route in `MapPublicDiscovery` ~121; new handler near `RemoveOrganizerParticipantAsync` ~340; new request record near the DTOs ~890)
- Test: `backend/Kasanie.Tests/AuthorizationIntegrationTests.cs`

**Interfaces:**
- Consumes: `ResolveParticipantPlacement` (Task 1).
- Produces: `POST /api/organizer/activities/{id:int}/participants` → `200 { participantId: long, status: string }`; request `AddParticipantRequest(string? Name, string? Contact)`.

- [ ] **Step 1: Write the failing tests** in `AuthorizationIntegrationTests.cs`:

```csharp
[Fact]
public async Task AddOrganizerParticipant_PlacesConfirmedThenWaitlistThenRejects()
{
    await using var factory = new TestApplicationFactory();
    await factory.SeedAsync(db =>
    {
        SeedPublicActivity(db, "organizer-a");
        var a = db.PublicActivities.Local.Single();
        a.Capacity = 1; a.WaitlistCapacity = 1;
    });
    using var client = factory.CreateClient();
    var csrf = await CsrfAsync(client, "organizer-a", Roles.Organizer);

    using var first = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants",
        new { name = "Пешеход Один", contact = "walkin-1@example.test" }, "organizer-a", Roles.Organizer, csrf));
    using var second = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants",
        new { name = "Пешеход Два", contact = "+7 920 000 00 02" }, "organizer-a", Roles.Organizer, csrf));
    using var third = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants",
        new { name = "Пешеход Три" }, "organizer-a", Roles.Organizer, csrf));

    Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    Assert.Equal("Confirmed", JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());
    Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    Assert.Equal("Waitlisted", JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());
    Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);

    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Assert.Equal(2, await db.PublicActivityParticipants.CountAsync(x => x.Source == "organizer-added"));
    Assert.True(await db.PublicActivities.AnyAsync(x => x.Id == 1 && x.Status == PublicActivityStatus.Full));
}

[Fact]
public async Task AddOrganizerParticipant_RejectsForNonOwnerAndDuplicateContact()
{
    await using var factory = new TestApplicationFactory();
    await factory.SeedAsync(db => SeedPublicActivity(db, "organizer-a"));
    using var owner = factory.CreateClient();
    var ownerCsrf = await CsrfAsync(owner, "organizer-a", Roles.Organizer);
    using var stranger = factory.CreateClient();
    var strangerCsrf = await CsrfAsync(stranger, "organizer-b", Roles.Organizer);

    using var foreign = await stranger.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants",
        new { name = "Чужой", contact = "x@example.test" }, "organizer-b", Roles.Organizer, strangerCsrf));
    using var ok = await owner.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants",
        new { name = "Первый", contact = "dup@example.test" }, "organizer-a", Roles.Organizer, ownerCsrf));
    using var dup = await owner.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants",
        new { name = "Второй", contact = "DUP@example.test" }, "organizer-a", Roles.Organizer, ownerCsrf));

    Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
}
```

- [ ] **Step 2: Run** the two tests. Expected: FAIL (404 route not mapped).

- [ ] **Step 3: Map the route** in `MapPublicDiscovery`, next to the existing participants routes:

```csharp
organizerApi.MapPost("/{id:int}/participants", AddOrganizerParticipantAsync);
```

- [ ] **Step 4: Add the request record** near the other request records at the bottom of the class:

```csharp
public sealed record AddParticipantRequest(string? Name, string? Contact);
```

- [ ] **Step 5: Implement `AddOrganizerParticipantAsync`** near `RemoveOrganizerParticipantAsync`:

```csharp
private static async Task<IResult> AddOrganizerParticipantAsync(
    int id, AddParticipantRequest request, ClaimsPrincipal principal, AppDbContext db, IAuditService audit, IConfiguration configuration)
{
    if (!PublicDiscoveryEnabled(configuration)) return Results.NotFound();
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var name = request.Name?.Trim() ?? string.Empty;
    var contact = request.Contact?.Trim() ?? string.Empty;
    var errors = new Dictionary<string, string[]>();
    if (name.Length is < 2 or > 80) errors["name"] = ["Укажите имя от 2 до 80 символов."];
    if (contact.Length is > 120) errors["contact"] = ["Контакт не длиннее 120 символов."];
    if (contact.Length is > 0 and < 3) errors["contact"] = ["Контакт от 3 до 120 символов."];
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    await using var transaction = db.Database.IsRelational()
        ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
        : null;
    var activity = await db.PublicActivities.Include(x => x.Participants)
        .SingleOrDefaultAsync(x => x.Id == id && x.OrganizerId == userId);
    if (activity is null) return Results.Forbid();
    if (activity.Status is PublicActivityStatus.Cancelled or PublicActivityStatus.Completed or PublicActivityStatus.Archived)
        return Results.Conflict(new { message = "К этой активности больше нельзя добавлять участников." });

    string? contactHash = null;
    PublicActivityParticipant? existing = null;
    if (contact.Length > 0)
    {
        var contactKey = Regex.Replace(contact.ToLowerInvariant(), "\\s+", string.Empty);
        contactHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contactKey)));
        existing = activity.Participants.SingleOrDefault(x => x.GuestContactHash == contactHash);
        if (existing is not null && existing.Status != PublicParticipantStatus.Cancelled)
            return Results.Conflict(new { message = "Этот участник уже в списке." });
    }

    var placement = ResolveParticipantPlacement(activity);
    if (placement.RejectionMessage is not null) return Results.Conflict(new { message = placement.RejectionMessage });

    var participant = existing ?? new PublicActivityParticipant { GuestContactHash = contactHash };
    participant.GuestName = name;
    participant.GuestContact = contact.Length > 0 ? contact : null;
    participant.Status = placement.Status;
    participant.JoinedAt = DateTimeOffset.UtcNow;
    participant.ConfirmedAt = placement.Status == PublicParticipantStatus.Confirmed ? DateTimeOffset.UtcNow : null;
    participant.CancelledAt = null;
    participant.Source = "organizer-added";
    if (existing is null) activity.Participants.Add(participant);
    if (placement.BecomesFull) activity.Status = PublicActivityStatus.Full;
    activity.Version++;
    activity.UpdatedAt = DateTimeOffset.UtcNow;
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateException) { return Results.Conflict(new { message = "Состояние записи изменилось. Обновите страницу и попробуйте снова." }); }
    await audit.WriteAsync(userId, "public_activity_participant_added", nameof(PublicActivity), id.ToString(), $"status:{placement.Status}");
    if (transaction is not null) await transaction.CommitAsync();
    return Results.Ok(new { participantId = participant.Id, status = placement.Status.ToString() });
}
```

- [ ] **Step 6: Run** the full suite. Expected: `Passed: 82` (78 + 2 new tests, each with multiple asserts counts once).

- [ ] **Step 7: Commit** (on user authorization)

```bash
git add backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs backend/Kasanie.Tests/AuthorizationIntegrationTests.cs
git commit -m "feat(sports): organizer can manually add a participant"
```

---

### Task 3: `PublicActivityParticipantReport` entity + migration

**Files:**
- Modify: `backend/Kasanie.Api/Domain/Models.cs` (new enums + entity near `PublicActivityParticipant` ~631)
- Modify: `backend/Kasanie.Api/Infrastructure/AppDbContext.cs` (DbSet ~51; fluent config ~96)
- Create: `backend/Kasanie.Api/Infrastructure/Migrations/*_AddPublicActivityParticipantReports.cs` (+ Designer + snapshot update — generated)
- Test: `backend/Kasanie.Tests/AuthorizationIntegrationTests.cs` (persistence smoke)

**Interfaces:**
- Produces: `enum ParticipantReportReason { NoShow, LateArrival, AggressiveOrConflict, UnsafePlay, Other }`; `enum ParticipantReportStatus { Active, Retracted }`; `class PublicActivityParticipantReport` with `long Id, int PublicActivityId, long? PublicActivityParticipantId, string AuthorOrganizerId, string? SubjectUserId, string? SubjectGuestContactHash, ParticipantReportReason Reason, string Comment, ParticipantReportStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt`; `DbSet<PublicActivityParticipantReport> PublicActivityParticipantReports`.

- [ ] **Step 1: Add enums** to `Models.cs` after the `PublicParticipantStatus` enum (line ~29):

```csharp
public enum ParticipantReportReason { NoShow, LateArrival, AggressiveOrConflict, UnsafePlay, Other }
public enum ParticipantReportStatus { Active, Retracted }
```

- [ ] **Step 2: Add the entity** after `PublicActivityParticipant` (line ~631):

```csharp
public sealed class PublicActivityParticipantReport
{
    public long Id { get; set; }
    public int PublicActivityId { get; set; }
    public PublicActivity Activity { get; set; } = null!;
    public long? PublicActivityParticipantId { get; set; }
    public required string AuthorOrganizerId { get; set; }
    public string? SubjectUserId { get; set; }
    public string? SubjectGuestContactHash { get; set; }
    public ParticipantReportReason Reason { get; set; }
    public required string Comment { get; set; }
    public ParticipantReportStatus Status { get; set; } = ParticipantReportStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
```

- [ ] **Step 3: Add the DbSet** in `AppDbContext.cs` after `PublicActivityParticipants` (line ~51):

```csharp
public DbSet<PublicActivityParticipantReport> PublicActivityParticipantReports => Set<PublicActivityParticipantReport>();
```

- [ ] **Step 4: Add fluent config** in `OnModelCreating` after the participant indexes (line ~96):

```csharp
builder.Entity<PublicActivityParticipantReport>().Property(x => x.Comment).HasMaxLength(2000);
builder.Entity<PublicActivityParticipantReport>().HasIndex(x => x.SubjectUserId).HasFilter("\"SubjectUserId\" IS NOT NULL");
builder.Entity<PublicActivityParticipantReport>().HasIndex(x => x.SubjectGuestContactHash).HasFilter("\"SubjectGuestContactHash\" IS NOT NULL");
builder.Entity<PublicActivityParticipantReport>()
    .HasIndex(x => new { x.AuthorOrganizerId, x.PublicActivityId, x.SubjectUserId, x.SubjectGuestContactHash }).IsUnique();
builder.Entity<PublicActivityParticipantReport>().ToTable(t => t.HasCheckConstraint(
    "CK_ParticipantReport_OneSubject",
    "(\"SubjectUserId\" IS NOT NULL) <> (\"SubjectGuestContactHash\" IS NOT NULL)"));
builder.Entity<PublicActivityParticipantReport>().HasOne(x => x.Activity).WithMany()
    .HasForeignKey(x => x.PublicActivityId);
```

(The global `foreach` sets `DeleteBehavior.Restrict` on the FK afterward. `AuthorOrganizerId` / `SubjectUserId` are `string` FKs to `ApplicationUser` — EF infers them by convention only if a navigation exists; here there is none, so add explicit no-navigation FKs:)

```csharp
builder.Entity<PublicActivityParticipantReport>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorOrganizerId);
builder.Entity<PublicActivityParticipantReport>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SubjectUserId).IsRequired(false);
```

- [ ] **Step 5: Generate the migration** in the container (from `backend/`):

```bash
WINPATH=$(pwd -W)
MSYS_NO_PATHCONV=1 docker run --rm -v "${WINPATH}:/src" -v kasanie-nuget:/root/.nuget/packages -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -lc \
  "dotnet tool install --global dotnet-ef >/dev/null 2>&1; export PATH=\$PATH:/root/.dotnet/tools; \
   dotnet ef migrations add AddPublicActivityParticipantReports --project Kasanie.Api/Kasanie.Api.csproj --output-dir Infrastructure/Migrations"
```

Inspect the generated `Up`: expect `CreateTable` + 3 `CreateIndex` + the check constraint in `table.CheckConstraint(...)`.

- [ ] **Step 6: Persistence smoke test** in `AuthorizationIntegrationTests.cs`:

```csharp
[Fact]
public async Task ParticipantReport_PersistsAndFiltersByStatus()
{
    await using var factory = new TestApplicationFactory();
    await factory.SeedAsync(db =>
    {
        SeedPublicActivity(db, "organizer-a");
        db.PublicActivityParticipantReports.Add(new PublicActivityParticipantReport
        {
            PublicActivityId = 1, AuthorOrganizerId = "organizer-a", SubjectUserId = "subject-x",
            Reason = ParticipantReportReason.NoShow, Comment = "Не пришёл без предупреждения"
        });
    });
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var report = await db.PublicActivityParticipantReports.SingleAsync();
    Assert.Equal(ParticipantReportStatus.Active, report.Status);
    Assert.Equal("subject-x", report.SubjectUserId);
}
```

- [ ] **Step 7: Run** the full suite. Expected: `Passed: 83`.

- [ ] **Step 8: Commit** (on user authorization)

```bash
git add backend/Kasanie.Api/Domain/Models.cs backend/Kasanie.Api/Infrastructure/ backend/Kasanie.Tests/AuthorizationIntegrationTests.cs
git commit -m "feat(sports): add PublicActivityParticipantReport entity and migration"
```

---

### Task 4: Report endpoints — file / retract / list

**Files:**
- Create: `backend/Kasanie.Api/Endpoints/PublicDiscoveryReportsEndpoints.cs`
- Modify: `backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs` (call `this.MapPublicDiscoveryReports()` inside `MapPublicDiscovery` end ~126)
- Test: `backend/Kasanie.Tests/AuthorizationIntegrationTests.cs`

**Interfaces:**
- Consumes: entity/enums from Task 3.
- Produces:
  - `POST /api/organizer/activities/{id:int}/participants/{participantId:long}/reports` body `{ reason: string, comment: string }` → `201 { reportId: long }`
  - `DELETE /api/organizer/activities/{id:int}/reports/{reportId:long}` → `204`
  - `GET  /api/organizer/activities/{id:int}/participants/{participantId:long}/reports` → `200 [{ reason, comment, createdAt, isMine, sourceActivityTitle }]`

- [ ] **Step 1: Write failing tests** in `AuthorizationIntegrationTests.cs`:

```csharp
[Fact]
public async Task FileParticipantReport_OnlyAfterActivityEndsAndByOwner_ThenVisibleCrossActivity()
{
    await using var factory = new TestApplicationFactory();
    await factory.SeedAsync(db =>
    {
        SeedPublicActivity(db, "organizer-a"); // activity 1, StartAt +2d (future)
        var future = db.PublicActivities.Local.Single();
        db.PublicActivities.Add(new PublicActivity
        {
            Id = 2, Slug = "past-football", SportId = 1, SportsVenueId = 1, OrganizerId = "organizer-b",
            EventType = PublicActivityType.Game, Title = "Прошедшая игра", Description = "x",
            StartAt = DateTimeOffset.UtcNow.AddDays(-2), EndAt = DateTimeOffset.UtcNow.AddDays(-2).AddHours(2),
            Capacity = 10, Status = PublicActivityStatus.Published, PublishedAt = DateTimeOffset.UtcNow.AddDays(-5)
        });
        db.PublicActivityParticipants.AddRange(
            new PublicActivityParticipant { Id = 70, PublicActivityId = 1, UserId = "player-x", Status = PublicParticipantStatus.Confirmed, JoinedAt = DateTimeOffset.UtcNow },
            new PublicActivityParticipant { Id = 71, PublicActivityId = 2, UserId = "player-x", Status = PublicParticipantStatus.Attended, JoinedAt = DateTimeOffset.UtcNow.AddDays(-3) });
    });
    using var orgA = factory.CreateClient();
    var csrfA = await CsrfAsync(orgA, "organizer-a", Roles.Organizer);
    using var orgB = factory.CreateClient();
    var csrfB = await CsrfAsync(orgB, "organizer-b", Roles.Organizer);

    // organizer-a's activity 1 is in the future → 409
    using var tooEarly = await orgA.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants/70/reports",
        new { reason = "NoShow", comment = "Не пришёл" }, "organizer-a", Roles.Organizer, csrfA));
    Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);

    // organizer-b files on the finished activity 2 → 201
    using var filed = await orgB.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/2/participants/71/reports",
        new { reason = "AggressiveOrConflict", comment = "Конфликт с соперником" }, "organizer-b", Roles.Organizer, csrfB));
    Assert.Equal(HttpStatusCode.Created, filed.StatusCode);

    // non-owner organizer-a cannot file on activity 2
    using var foreignFile = await orgA.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/2/participants/71/reports",
        new { reason = "Other", comment = "нет" }, "organizer-a", Roles.Organizer, csrfA));
    Assert.Equal(HttpStatusCode.Forbidden, foreignFile.StatusCode);

    // organizer-a sees the cross-activity report for player-x via participant 70 on activity 1
    using var list = await orgA.SendAsync(Get("/api/organizer/activities/1/participants/70/reports", "organizer-a", Roles.Organizer));
    Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
    Assert.Equal(1, listJson.RootElement.GetArrayLength());
    Assert.False(listJson.RootElement[0].GetProperty("isMine").GetBoolean());
    Assert.Equal("Прошедшая игра", listJson.RootElement[0].GetProperty("sourceActivityTitle").GetString());
}

[Fact]
public async Task RetractParticipantReport_RemovesItFromCountAndList()
{
    await using var factory = new TestApplicationFactory();
    long reportId = 0;
    await factory.SeedAsync(db =>
    {
        SeedPublicActivity(db, "organizer-a");
        db.PublicActivities.Local.Single().StartAt = DateTimeOffset.UtcNow.AddDays(-1);
        db.PublicActivities.Local.Single().EndAt = DateTimeOffset.UtcNow.AddHours(-2);
        db.PublicActivityParticipants.Add(new PublicActivityParticipant { Id = 72, PublicActivityId = 1, UserId = "player-y", Status = PublicParticipantStatus.Attended, JoinedAt = DateTimeOffset.UtcNow.AddDays(-2) });
    });
    using var client = factory.CreateClient();
    var csrf = await CsrfAsync(client, "organizer-a", Roles.Organizer);
    using var filed = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants/72/reports",
        new { reason = "NoShow", comment = "Отменил в последний момент" }, "organizer-a", Roles.Organizer, csrf));
    reportId = JsonDocument.Parse(await filed.Content.ReadAsStringAsync()).RootElement.GetProperty("reportId").GetInt64();

    using var dup = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants/72/reports",
        new { reason = "Other", comment = "ещё раз" }, "organizer-a", Roles.Organizer, csrf));
    Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

    using var retract = await client.SendAsync(JsonRequest(HttpMethod.Delete, $"/api/organizer/activities/1/reports/{reportId}",
        new { }, "organizer-a", Roles.Organizer, csrf));
    Assert.Equal(HttpStatusCode.NoContent, retract.StatusCode);

    using var list = await client.SendAsync(Get("/api/organizer/activities/1/participants/72/reports", "organizer-a", Roles.Organizer));
    Assert.Equal(0, JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetArrayLength());

    // after retract, filing again is allowed
    using var refile = await client.SendAsync(JsonRequest(HttpMethod.Post, "/api/organizer/activities/1/participants/72/reports",
        new { reason = "LateArrival", comment = "Опоздал на час" }, "organizer-a", Roles.Organizer, csrf));
    Assert.Equal(HttpStatusCode.Created, refile.StatusCode);
}
```

- [ ] **Step 2: Run** — expect FAIL (routes not mapped).

- [ ] **Step 3: Create `PublicDiscoveryReportsEndpoints.cs`:**

```csharp
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
        p.UserId is not null ? (p.UserId, null)
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
        await db.SaveChangesAsync();
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
            .Select(r => new { r.Reason, r.Comment, r.CreatedAt, r.AuthorOrganizerId, r.PublicActivityId })
            .ToListAsync();
        var titles = await db.PublicActivities.AsNoTracking()
            .Where(a => reports.Select(r => r.PublicActivityId).Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Title);
        return Results.Ok(reports.Select(r => new
        {
            reason = r.Reason.ToString(),
            r.Comment,
            r.CreatedAt,
            isMine = r.AuthorOrganizerId == userId,
            sourceActivityTitle = titles.GetValueOrDefault(r.PublicActivityId, "")
        }));
    }

    public sealed record ParticipantReportRequest(string? Reason, string? Comment);
}
```

- [ ] **Step 4: Wire the mapping** — at the end of `MapPublicDiscovery` in `PublicDiscoveryEndpoints.cs`:

```csharp
app.MapPublicDiscoveryReports();
```

- [ ] **Step 5: Run** the full suite. Expected: `Passed: 85`.

- [ ] **Step 6: Commit** (on user authorization)

```bash
git add backend/Kasanie.Api/Endpoints/ backend/Kasanie.Tests/AuthorizationIntegrationTests.cs
git commit -m "feat(sports): file/retract/list participant reports"
```

---

### Task 5: Badge count in `GetOrganizerParticipantsAsync`

**Files:**
- Modify: `backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs` (`GetOrganizerParticipantsAsync` ~320; `OrganizerParticipantDto` ~890)
- Test: `backend/Kasanie.Tests/AuthorizationIntegrationTests.cs`

**Interfaces:**
- Consumes: entity from Task 3.
- Produces: `OrganizerParticipantDto` gains `int ReportCount, bool ViewerHasReported` as the last two positional members; `GET /api/organizer/activities/{id}/participants` items carry `reportCount`, `viewerHasReported`.

- [ ] **Step 1: Failing test:**

```csharp
[Fact]
public async Task OrganizerParticipants_ShowCrossOrganizerReportBadge()
{
    await using var factory = new TestApplicationFactory();
    await factory.SeedAsync(db =>
    {
        SeedPublicActivity(db, "organizer-a");
        db.PublicActivityParticipants.Add(new PublicActivityParticipant { Id = 80, PublicActivityId = 1, UserId = "flagged-user", Status = PublicParticipantStatus.Confirmed, JoinedAt = DateTimeOffset.UtcNow });
        db.PublicActivityParticipantReports.AddRange(
            new PublicActivityParticipantReport { PublicActivityId = 1, AuthorOrganizerId = "organizer-b", SubjectUserId = "flagged-user", Reason = ParticipantReportReason.NoShow, Comment = "не пришёл" },
            new PublicActivityParticipantReport { PublicActivityId = 1, AuthorOrganizerId = "organizer-c", SubjectUserId = "flagged-user", Reason = ParticipantReportReason.UnsafePlay, Comment = "жёстко играл", Status = ParticipantReportStatus.Retracted });
    });
    using var client = factory.CreateClient();
    using var list = await client.SendAsync(Get("/api/organizer/activities/1/participants", "organizer-a", Roles.Organizer));
    using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
    var row = json.RootElement.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == 80);
    Assert.Equal(1, row.GetProperty("reportCount").GetInt32()); // retracted one excluded
    Assert.False(row.GetProperty("viewerHasReported").GetBoolean());
}
```

- [ ] **Step 2: Run** — expect FAIL (`reportCount` property missing).

- [ ] **Step 3: Extend the DTO** (`OrganizerParticipantDto`):

```csharp
public sealed record OrganizerParticipantDto(long Id, string DisplayName, string? Contact, string Status,
    DateTimeOffset JoinedAt, DateTimeOffset? ConfirmedAt, DateTimeOffset? CancelledAt,
    int ReportCount, bool ViewerHasReported);
```

- [ ] **Step 4: Augment `GetOrganizerParticipantsAsync`** — after the `names` line:

```csharp
var callerId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
var subjectUserIds = activity.Participants.Where(p => p.UserId != null).Select(p => p.UserId!).ToArray();
var subjectHashes = activity.Participants.Where(p => p.GuestContactHash != null).Select(p => p.GuestContactHash!).ToArray();
var reportRows = await db.PublicActivityParticipantReports.AsNoTracking()
    .Where(r => r.Status == ParticipantReportStatus.Active &&
        ((r.SubjectUserId != null && subjectUserIds.Contains(r.SubjectUserId)) ||
         (r.SubjectGuestContactHash != null && subjectHashes.Contains(r.SubjectGuestContactHash))))
    .Select(r => new { r.SubjectUserId, r.SubjectGuestContactHash, r.AuthorOrganizerId })
    .ToListAsync();
int CountFor(PublicActivityParticipant p) => reportRows.Count(r =>
    (p.UserId != null && r.SubjectUserId == p.UserId) || (p.GuestContactHash != null && r.SubjectGuestContactHash == p.GuestContactHash));
bool MineFor(PublicActivityParticipant p) => reportRows.Any(r => r.AuthorOrganizerId == callerId &&
    ((p.UserId != null && r.SubjectUserId == p.UserId) || (p.GuestContactHash != null && r.SubjectGuestContactHash == p.GuestContactHash)));
```

Then in the `.Select(x => new OrganizerParticipantDto(...))` append `CountFor(x), MineFor(x)`.

- [ ] **Step 5: Run** the full suite. Expected: `Passed: 86`.

- [ ] **Step 6: Commit** (on user authorization)

```bash
git add backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs backend/Kasanie.Tests/AuthorizationIntegrationTests.cs
git commit -m "feat(sports): surface participant report badge to organizer"
```

---

### Task 6: Frontend — add-participant form, report badge + form

**Files:**
- Modify: `frontend/src/pages/SportsNearbyPages.tsx` (`OrganizerParticipant` type ~18; `participants-manager` JSX ~708; handlers ~687-703)
- Test: `frontend/src/test/workflows.test.tsx`

**Interfaces:**
- Consumes: the three report endpoints + the add-participant endpoint + the extended participants payload.

- [ ] **Step 1: Failing vitest** in `workflows.test.tsx` — organizer opens Participants, sees a `!` badge for a flagged row, expands it, sees a report; and adds a participant via the form. Mock:
  - `GET /api/organizer/activities/` → one Published activity `{ id: 1, ... , endAt: <past ISO> }`
  - `GET /api/organizer/activities/1/participants` → `{ activityId:1, capacity:10, confirmedCount:1, waitlistedCount:0, cancelledCount:0, items:[{ id:80, displayName:'Игрок Икс', contact:null, status:'Confirmed', joinedAt:'2026-09-01T10:00:00Z', reportCount:1, viewerHasReported:false }] }`
  - `GET /api/organizer/activities/1/participants/80/reports` → `[{ reason:'NoShow', comment:'не пришёл', createdAt:'2026-09-02T10:00:00Z', isMine:false, sourceActivityTitle:'Прошлая игра' }]`
  - `POST /api/organizer/activities/1/participants` → `{ participantId: 99, status:'Confirmed' }`
  Assert: badge button present; after click, "не пришёл" text visible; after filling the add form and submitting, `POST /api/organizer/activities/1/participants` was called.

- [ ] **Step 2: Run** — expect FAIL.

- [ ] **Step 3: Types + labels** — extend `OrganizerParticipant`:

```ts
type OrganizerParticipant = { id: number; displayName: string; contact?: string; status: string; joinedAt: string; confirmedAt?: string; cancelledAt?: string; reportCount: number; viewerHasReported: boolean }
type ParticipantReport = { reason: string; comment: string; createdAt: string; isMine: boolean; sourceActivityTitle: string }
const reportReasonLabels: Record<string, string> = { NoShow: 'Не пришёл', LateArrival: 'Опоздал', AggressiveOrConflict: 'Агрессия или конфликт', UnsafePlay: 'Опасная игра', Other: 'Другое' }
```

- [ ] **Step 4: Handlers** in `OrganizerActivitiesPage` (near `removeParticipant`):

```ts
const addParticipant = async (event: FormEvent<HTMLFormElement>) => {
  event.preventDefault()
  if (!participantActivity) return
  const form = event.currentTarget
  const values = new FormData(form)
  setParticipantLoading(true); setMessage(null)
  try {
    await post(`/api/organizer/activities/${participantActivity.id}/participants`, { name: values.get('name'), contact: values.get('contact') || null })
    form.reset()
    setParticipants(await api<OrganizerParticipants>(`/api/organizer/activities/${participantActivity.id}/participants`))
    setMessage({ text: 'Участник добавлен.', ok: true }); await reload()
  } catch (error) { setMessage({ text: error instanceof Error ? error.message : 'Не удалось добавить участника.', ok: false }) }
  finally { setParticipantLoading(false) }
}

const fileReport = async (participantId: number, reason: string, comment: string) => {
  if (!participantActivity) return
  await post(`/api/organizer/activities/${participantActivity.id}/participants/${participantId}/reports`, { reason, comment })
  setParticipants(await api<OrganizerParticipants>(`/api/organizer/activities/${participantActivity.id}/participants`))
}

const retractReport = async (reportId: number) => {
  if (!participantActivity) return
  await remove(`/api/organizer/activities/${participantActivity.id}/reports/${reportId}`)
}
```

(Report list per row is fetched lazily via `api<ParticipantReport[]>(\`/api/organizer/activities/${participantActivity.id}/participants/${participantId}/reports\`)` inside a small expandable subcomponent; retract has no `reportId` in the list payload — add `reportId` to the list DTO in Task 4's `GetParticipantReportsAsync` select and the test, or drop the retract affordance from the list. **Decision: add `reportId` + `isMine` gate to the list payload.**)

- [ ] **Step 5: JSX** — in each participant `<article>` of `participants-manager`:
  - if `participant.reportCount > 0`: a `<button class="report-badge">!</button>` that toggles an expandable `<ParticipantReports>` panel (lazy-fetches the list, renders `reportReasonLabels[reason]` + comment + `sourceActivityTitle` + date; for `isMine` rows a "Отозвать" button → `retractReport`).
  - if `new Date(participantActivity.startAt) < new Date()` (activity past): a small "Пожаловаться" form (reason `<select>` from `reportReasonLabels`, `<textarea>` comment) → `fileReport`.
  - Above the list: an "Добавить участника" `<form onSubmit={addParticipant}>` — `name` input (required, 2..80), `contact` input (optional).

- [ ] **Step 6: Run** `npm run lint && npx tsc --noEmit && npm run test`. Expected: all pass, including the new test and the existing 24.

- [ ] **Step 7: Commit** (on user authorization)

```bash
git add frontend/src/pages/SportsNearbyPages.tsx frontend/src/test/workflows.test.tsx
git commit -m "feat(sports): organizer roster — add participant, complaint badge and form"
```

---

### Task 7: Full verification + docs

- [ ] **Step 1:** Backend container suite green (expect ~86). Frontend `lint` + `tsc` + `test` + `build` green.
- [ ] **Step 2:** `superpowers:requesting-code-review` on the 1C diff (if session limits allow; else inline review).
- [ ] **Step 3:** Update `docs/ROADMAP.md` §1C — mark «Управление составом участников» done (with the note that the participant-complaint mechanism was pulled forward from 1D), list new endpoints/entity, test count.
- [ ] **Step 4:** `superpowers:verification-before-completion`; report to user; pause before sub-project 3.
- [ ] **Step 5:** Learning-loop: note the `PublicActivityParticipantReport` cross-activity identity-key pattern in memory if it proves reusable for 1D.

## Self-review notes

- Spec coverage: A (Task 2) + helper (Task 1); entity/migration (Task 3); endpoints (Task 4); badge (Task 5); UI (Task 6); verification/docs (Task 7). All spec sections mapped.
- Task 4 Step 4 note: `GetParticipantReportsAsync` select must also emit `reportId` (needed by the Task 6 retract affordance) — add `r.Id` → `reportId` to that projection and to the `FileParticipantReport...` list assertion.
- Type consistency: `OrganizerParticipantDto` positional order fixed in Task 5; frontend `OrganizerParticipant` mirrors it in Task 6. `ParticipantReportRequest` / `AddParticipantRequest` names stable across tasks.
- InMemory provider ignores the CHECK constraint — the "both subjects set" rejection is only enforced against Postgres; note in Task 3, do not write an InMemory test asserting it.
