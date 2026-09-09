---
name: arch-1c-participant-reports
description: Kasanie "Спорт рядом" 1C — manual-add participant + post-activity participant complaint (cross-organizer reputation signal); design analysis
metadata:
  type: project
---

Sub-project 1C of "Спорт рядом". Two features scoped 2026-09-09.

**Feature A — organizer manually adds a walk-up participant.** Guest-style `PublicActivityParticipant` row, `Source = "organizer-added"`, no `UserId`. Same SHA-256 contact-hash scheme as `JoinGuestPublicActivityAsync` so identity matches if the person later self-registers as a guest. Two-tier Confirmed/Waitlisted rule, dup-contact reuse of a `Cancelled` row — mirror the guest-join path.

**Feature B — post-activity participant complaint.** New entity (greenfield — no Report/Complaint/Reputation type exists today). Reason enum + free text. Targets either a registered `UserId` or a guest `GuestContactHash`. Organizer-only visibility; surfaced as a "!" badge per participant row in `GetOrganizerParticipantsAsync`, expanding shows all complaints about that person across all organizers/activities. No participant notification, no moderation (that is 1D). Audit-log the filing.

**Non-obvious facts that shaped the recommendation:**
- `PublicActivityStatus.Completed` is **never set anywhere** in the codebase — no background job transitions activities. So the "activity took place" gate for filing a complaint is effectively `activity.EndAt < DateTimeOffset.UtcNow` only.
- Activities are **never row-deleted** — `DeletePublicActivityAsync` sets `Status = Archived` (soft). Participant rows also survive (cancel = status change). So a durable report FK to activity/participant is safe in practice.
- **Global `DeleteBehavior.Restrict` on every FK** (`AppDbContext.OnModelCreating` foreach at ~line 113). New report FKs inherit Restrict automatically — no cascade to worry about, and an accidental hard-delete of a referenced activity would be blocked.
- Enum storage: only `SkillCategory` is a Postgres enum (`HasPostgresEnum`). All other enums (`PublicParticipantStatus`, `PublicActivityStatus`) persist as **int** by EF convention — a new `ParticipantReportReason` enum stored as int is the consistent choice.
- `CoachNote` (Domain/Models.cs ~633) is the precedent for a minimal note-like entity: int Id, FK ints, `required string Text`, `CreatedAt`, and **no fluent config at all** — relies on conventions + the global Restrict.
- Endpoint norm: one `public static partial class EndpointMapping`, ~10 files, no service layer, DI injected per-handler. New partial-class file `PublicDiscoveryReportsEndpoints.cs` with its own `MapPublicDiscoveryReports` called from `MapPublicDiscovery`/`EndpointMapping` fits the norm; a real service does not.
- `GetOrganizerParticipantsAsync` is `AsNoTracking`, loads `activity.Participants` (no `User` include), batches display names via `ResolveParticipantNamesAsync` (3 dictionary queries over Players/CoachProfiles/PublicOrganizerProfiles). The badge lookup adds one more batched query: `WHERE SubjectUserId IN (...) OR SubjectGuestContactHash IN (...)`.
- Migrations are named `Add<Thing>`, plain EF up/down, no data seed. Next would be `AddPublicParticipantReports`.
- Only one integration-test file exercises this area: `backend/Kasanie.Tests/AuthorizationIntegrationTests.cs` (TestApplicationFactory, `SeedPublicActivity`, `RecordingEmailSender`, CSRF helper). Handlers guard transactions with `db.Database.IsRelational()` because the test provider is non-relational (InMemory) — Serializable tx and filtered unique indexes are skipped under test.
- **Roadmap places "механизм жалоб" in 1D, not 1C** (`docs/ROADMAP.md` line 71). Pulling Feature B forward into 1C is a product-owner scoping decision worth confirming.
- 152-ФЗ is a pre-launch blocker for "Спорт рядом" (`docs/ROADMAP.md` line 150-152), explicitly required before real users.
