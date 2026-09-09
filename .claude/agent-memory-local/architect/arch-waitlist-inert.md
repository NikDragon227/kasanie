---
name: arch-waitlist-inert
description: Kasanie public-activity waitlist — organizer form NOW exposes waitlistCapacity (memory updated 2026-09-09); still 0 by default
metadata:
  type: project
---

**Status changed as of 2026-09-09.** `frontend/src/pages/SportsNearbyPages.tsx` `OrganizerActivitiesPage` create/update form NOW has a real field `<input name="waitlistCapacity" min=0 max=500 defaultValue={editing?.waitlistCapacity ?? 0} />` and the payload sends `waitlistCapacity: Number(values.get('waitlistCapacity') ?? 0)` (~line 656). So an organizer can opt into a waitlist; it is no longer hard-wired to 0.

Default is still 0 (empty field → 0), so activities where the organizer leaves the field alone behave as before: overflow joiners get the 409 "Свободных мест и мест в листе ожидания больше нет" from both `JoinPublicActivityAsync` and `JoinGuestPublicActivityAsync`.

**How to apply:** Waitlist code paths are reachable from the UI now. When touching waitlist behavior you can exercise it end-to-end through the organizer form. A prod walkthrough will still show waitlist as a no-op for any activity created without touching that field.
