---
name: arch-public-discovery-participation
description: Kasanie "Спорт рядом" participation flow — join/leave/guest/organizer paths, PromoteFirstWaitlisted fan-out, transaction & occupancy conventions
metadata:
  type: project
---

File: `backend/Kasanie.Api/Endpoints/PublicDiscoveryEndpoints.cs` (~882 lines). Models: `PublicActivity`, `PublicActivityParticipant`, `PublicParticipantStatus` in `Domain/Models.cs` ~574-631. A participant row is either authenticated (`UserId`/`User` set) or guest (`GuestName`/`GuestContact`/`GuestContactHash`/`GuestCancellationTokenHash` set, `UserId` null, `Source="guest-web"`).

**Occupancy math is derived, not stored.** "confirmed" = count of `Confirmed or Attended`; "waitlisted" = count of `Waitlisted`. `RefreshPublicActivityOccupancy(activity)` recomputes `Published` vs `Full` from confirmed>=Capacity and bumps `Version`/`UpdatedAt`. `ToPublicActivity` exposes `AvailablePlaces = Capacity-confirmed`, `WaitlistAvailablePlaces = WaitlistCapacity-waitlisted`.

**PromoteFirstWaitlisted(activity)** picks oldest `Waitlisted` by `JoinedAt`, sets `Confirmed`+`ConfirmedAt`, returns the row (or null). Called from 4 sites, all after releasing a confirmed place:
- `LeavePublicActivityAsync`
- `CancelGuestPublicParticipationAsync`
- `RemoveOrganizerParticipantAsync`
- organizer-leaves branch of `UpdatePublicActivityAsync` (return value ignored there)
Each site then writes audit `public_activity_waitlist_promoted` for `promoted.UserId`. No email is sent on promotion today.

**Transaction convention:** join/leave/guest-cancel/remove-participant open `db.Database.BeginTransactionAsync(IsolationLevel.Serializable)` (relational only), do work, `SaveChangesAsync`, audit, `CommitAsync`. `JoinPublicActivityAsync`/`JoinGuestPublicActivityAsync` also catch `DbUpdateException` → 409. **`CancelPublicActivityAsync` is the exception: NO explicit transaction** — it resolves recipient emails, sets `Cancelled`, `SaveChangesAsync`, audits, then loops `TrySendAsync` after save. This is the template to mirror for "send after commit".

**Guest vs authenticated join divergence (being unified):** `JoinPublicActivityAsync` puts overflow users on the waitlist up to `WaitlistCapacity`. `JoinGuestPublicActivityAsync` historically hard-refused with "Свободных мест больше нет" when `confirmed >= Capacity`. Product decision: guests follow the same waitlist rules; guests without an email address simply can't be notified on later promotion. Frontend already handles a `Waitlisted` guest status (`participationLabels`, `GuestParticipationPage` `active` list, guest manage page). Guest cancellation token is minted once at join and rotated on cancel — no need to re-mint on promotion.

**Recipient resolution pattern (from CancelPublicActivityAsync):** `x.User?.Email ?? (x.GuestContact?.Contains('@') == true ? x.GuestContact : null)`, filter null/whitespace. Requires `.Include(x => x.Participants).ThenInclude(x => x.User)` — the promote call sites currently do NOT include `User`, so adding promotion email needs that Include (or a targeted email lookup) on the 4 handlers.
