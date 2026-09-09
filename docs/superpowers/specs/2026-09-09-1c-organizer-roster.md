# 1C — Управление составом участников у организатора (design)

Sub-project 2 of Priority 1 «Спорт рядом». Two features on top of the existing
organizer Participants tab (`GET`/`DELETE /api/organizer/activities/{id}/participants`).

## Feature A — Manual add participant

Organizer adds a walk-up / phone signup by **name + optional contact**.

- `POST /api/organizer/activities/{id}/participants` — new `AddOrganizerParticipantAsync`
  in `PublicDiscoveryEndpoints.cs`, mapped on the existing `organizerApi` group.
- Body: `{ name: string (2..80), contact?: string (3..120) }`.
- Auth: activity must exist and `OrganizerId == caller`; else `Results.Forbid()`.
- Activity gate: reject `409` when `Status` is `Cancelled`, `Completed` or `Archived`.
- Placement: **two-tier**, identical to `JoinGuestPublicActivityAsync` — Confirmed if
  `confirmed < Capacity`, else Waitlisted if `waitlisted < WaitlistCapacity`, else `409`
  "Свободных мест и мест в листе ожидания больше нет.".
- Contact hashing: same normalization + SHA-256 hex as guest-join
  (`Regex.Replace(contact.ToLowerInvariant(), "\s+", "")` → `SHA256` hex). No contact →
  `GuestContactHash = null`, no dedup.
- Dedup (only when contact given): if an existing row for the same
  `(PublicActivityId, GuestContactHash)` is non-`Cancelled` → `409`
  "Этот участник уже в списке."; a `Cancelled` row is reused.
- Row: `GuestName = name`, `GuestContact = contact` (or null), `Source = "organizer-added"`,
  no cancellation token, `ConfirmedAt` set only when Confirmed. `Full` transition + `Version++`
  + `UpdatedAt` exactly as guest-join.
- Serializable transaction guarded by `db.Database.IsRelational()`, same as guest-join.
- Audit: `public_activity_participant_added`, `UserId = caller`, detail `status:{status}`.
- Response: `200 { participantId, status }`.
- An `organizer-added` row with an `@` contact receives the 1B waitlist-promotion email —
  intended (organizer entered a reachable contact on the person's behalf).

### Shared helper

Extract the placement math (currently inline in `JoinGuestPublicActivityAsync`) into a
pure private helper, used by both handlers:

```csharp
private readonly record struct ParticipantPlacement(
    PublicParticipantStatus Status, bool BecomesFull, string? RejectionMessage);

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

`JoinGuestPublicActivityAsync` keeps its own dedup check + strings and its token minting;
only the confirmed/waitlist/reject decision and the `Full` flag come from the helper.
Behavior for the guest path must stay byte-identical (same messages, same off-by-one).

## Feature B — Post-activity participant complaint

Cross-organizer reputation signal, **organizers only**, no participant notification, no
moderation flow (that stays in 1D).

### Entity — `PublicActivityParticipantReport`

| Column | Type | Notes |
|---|---|---|
| `Id` | `long` | PK |
| `PublicActivityId` | `int` | FK → `PublicActivity`, Restrict (global). The activity the complaint arose from. |
| `PublicActivityParticipantId` | `long?` | Soft pointer to the triggering row, UI convenience. FK, Restrict. Not the identity key. |
| `AuthorOrganizerId` | `string` | FK → `ApplicationUser`, Restrict. Who filed it. |
| `SubjectUserId` | `string?` | FK → `ApplicationUser`, Restrict. Set iff target is a registered user. |
| `SubjectGuestContactHash` | `string?` | Set iff target is a guest. Same SHA-256 hex scheme. |
| `Reason` | `ParticipantReportReason` (int enum) | `NoShow, LateArrival, AggressiveOrConflict, UnsafePlay, Other` |
| `Comment` | `string` | required, trimmed, 1..2000 |
| `Status` | `ParticipantReportStatus` (int enum) | `Active, Retracted`. Default `Active`. |
| `CreatedAt` | `DateTimeOffset` | |
| `UpdatedAt` | `DateTimeOffset?` | set on retract |

Fluent config in `AppDbContext.OnModelCreating`:

- `HasIndex(SubjectUserId).HasFilter("\"SubjectUserId\" IS NOT NULL")`
- `HasIndex(SubjectGuestContactHash).HasFilter("\"SubjectGuestContactHash\" IS NOT NULL")`
- `HasIndex(AuthorOrganizerId, PublicActivityId, SubjectUserId, SubjectGuestContactHash).IsUnique()`
  — one complaint per organizer per person per activity.
- `Property(Comment).HasMaxLength(2000)`
- `ToTable(t => t.HasCheckConstraint("CK_ParticipantReport_OneSubject",
  "(\"SubjectUserId\" IS NOT NULL) <> (\"SubjectGuestContactHash\" IS NOT NULL)"))`
  — first CHECK constraint in the codebase; one line; guards the reputation record.

Migration: `AddPublicActivityParticipantReports` (generated in the .NET 10 Docker container).

### Endpoints — new file `PublicDiscoveryReportsEndpoints.cs`

Same `public static partial class EndpointMapping`. `MapPublicDiscoveryReports(this IEndpointRouteBuilder app)`
called from `MapPublicDiscovery`. All on a new `organizerReportApi` group =
`/api/organizer/activities` with `RequireAuthorization()` + `RequireRateLimiting("public-action")`.

1. `POST /{id:int}/participants/{participantId:long}/reports` — `FileParticipantReportAsync`
   - Body `{ reason: string, comment: string }`.
   - Load activity `Include(Participants)`, check `OrganizerId == caller` else `Forbid`.
   - Gate: `activity.EndAt >= UtcNow && activity.Status != Completed` → `409`
     "Жалобу можно оставить только после завершения активности.".
   - Find `participant` by id in `activity.Participants`; missing → `404`.
   - Subject key: `participant.UserId` (→ `SubjectUserId`) else `participant.GuestContactHash`
     (→ `SubjectGuestContactHash`); if participant has neither → `409`
     "У этого участника нет контакта для жалобы.".
   - Reject `409` if the organizer already has a non-`Retracted` report for the same
     `(AuthorOrganizerId, PublicActivityId, subject)` → "Вы уже оставляли жалобу на этого участника.".
   - Validate: `reason` parses to enum; `comment` trimmed 1..2000.
   - Insert `Active` report. Audit `participant_report_filed`, entity `PublicActivityParticipantReport`,
     detail `activity:{id};reason:{reason}`.
   - `201 { reportId }`.
2. `DELETE /{id:int}/reports/{reportId:long}` — `RetractParticipantReportAsync`
   - Load report; `404` if missing. `Forbid` unless `AuthorOrganizerId == caller`.
   - Already `Retracted` → `204` (idempotent).
   - Set `Status = Retracted`, `UpdatedAt = UtcNow`. Audit `participant_report_retracted`.
   - `204`.
3. `GET /{id:int}/participants/{participantId:long}/reports` — `GetParticipantReportsAsync`
   - Load activity `Include(Participants)`, `OrganizerId == caller` else `Forbid`.
   - Resolve subject key from the participant row as above; none → `Ok([])`.
   - Return **all `Active`** reports for that subject key across every activity, newest first:
     `{ reason, comment, createdAt, isMine, sourceActivityTitle }` where `isMine =
     AuthorOrganizerId == caller` and `sourceActivityTitle` from a batched activity-title lookup.

### `GetOrganizerParticipantsAsync` augmentation

After loading `activity.Participants`:

```csharp
var subjectUserIds = activity.Participants.Where(p => p.UserId != null).Select(p => p.UserId!).ToArray();
var subjectHashes  = activity.Participants.Where(p => p.GuestContactHash != null).Select(p => p.GuestContactHash!).ToArray();
var reports = await db.PublicActivityParticipantReports.AsNoTracking()
    .Where(r => r.Status == ParticipantReportStatus.Active &&
        ((r.SubjectUserId != null && subjectUserIds.Contains(r.SubjectUserId)) ||
         (r.SubjectGuestContactHash != null && subjectHashes.Contains(r.SubjectGuestContactHash))))
    .Select(r => new { r.SubjectUserId, r.SubjectGuestContactHash, r.AuthorOrganizerId })
    .ToListAsync();
```

Group in memory; per participant compute `ReportCount` (all matching) and `ViewerHasReported`
(`AuthorOrganizerId == caller` among matches). Add both to `OrganizerParticipantDto`:

```csharp
public sealed record OrganizerParticipantDto(long Id, string DisplayName, string? Contact, string Status,
    DateTimeOffset JoinedAt, DateTimeOffset? ConfirmedAt, DateTimeOffset? CancelledAt,
    int ReportCount, bool ViewerHasReported);
```

`GetOrganizerParticipantsAsync` gains a `ClaimsPrincipal` it already has; no new route.

### Frontend — `SportsNearbyPages.tsx`

- `OrganizerParticipant` type gains `reportCount: number`, `viewerHasReported: boolean`.
- Participant row: when `reportCount > 0` show a `!` badge button; expand →
  fetch `GET .../participants/{id}/reports`, render list (reason label + comment + date,
  own ones marked, with a "Retract" action calling `DELETE .../reports/{reportId}`).
- "Оставить жалобу" action per row, shown only when `new Date(activity.endAt) < now`:
  reason `<select>` + comment `<textarea>` → `POST .../participants/{id}/reports`, then refetch.
- "Добавить участника" form above the list: name + optional contact →
  `POST /api/organizer/activities/{id}/participants`, then refetch.
- Reason labels: `NoShow`→«Не пришёл», `LateArrival`→«Опоздал»,
  `AggressiveOrConflict`→«Агрессия или конфликт», `UnsafePlay`→«Опасная игра», `Other`→«Другое».

## Out of scope (1D or later)

Moderation review queue; numeric reputation/rating aggregation; complaints on
activities/venues; editing a filed complaint (retract + refile only); participant
notification; retention/erasure job (152-ФЗ policy track).

## Decisions taken

- Retract-only, no edit.
- Manual add allowed for any non-Cancelled/Completed/Archived activity (incl. past-but-not-archived and Full).
  The `Full` status flip on a manual add only fires when the activity is `Published` — a `Draft` never
  becomes `Full` via manual add.
- `organizer-added` email contacts do receive promotion mail.
- CHECK constraint kept despite being a codebase first — cheap, guards reputation data.
- **The composite unique index from the table sketch above was dropped.** Over nullable
  `SubjectUserId`/`SubjectGuestContactHash` a Postgres unique index treats NULLs as distinct, so it would
  not have caught guest-hash duplicates, and a strict version would wrongly block retract + re-file.
  "One active complaint per organizer per person per activity" is enforced in `FileParticipantReportAsync`
  (`AnyAsync` filtered by `Status == Active`) inside the `Serializable` transaction, with a
  `catch (DbUpdateException) → 409` backstop. The index is now a plain lookup on
  `(AuthorOrganizerId, PublicActivityId)`.
- `PublicActivityParticipantId` is an **unconstrained soft pointer**, not a FK. Participant rows are never
  hard-deleted (status changes only), so an orphan pointer cannot arise; skipping the FK avoids a
  migration constraint that would never fire.
- `JoinPublicActivityAsync` (authenticated join) was also switched to `ResolveParticipantPlacement` so all
  three join paths share one implementation.
