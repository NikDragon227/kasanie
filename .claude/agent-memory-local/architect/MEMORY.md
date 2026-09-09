# Architect memory — Kasanie

## Architecture
- [EndpointMapping partial class](arch-endpointmapping.md) — one static partial class split across ~10 endpoint files; shared email helpers live in AuthEndpoints.cs
- [Public discovery participation flow](arch-public-discovery-participation.md) — join/leave/guest/organizer paths, PromoteFirstWaitlisted fan-out, transaction + occupancy conventions
- [Waitlist capacity now in organizer form](arch-waitlist-inert.md) — as of 2026-09-09 the organizer form exposes waitlistCapacity (defaults 0); paths reachable from UI
- [Спорт рядом sub-project 1C scoping](arch-1c-participant-reports.md) — manual-add participant + post-activity participant complaint/reputation signal; persistence & placement analysis
