---
name: arch-endpointmapping
description: How Kasanie.Api organizes minimal-API endpoints — one static partial class, shared email/URL helpers, no MediatR
metadata:
  type: project
---

`Kasanie.Api` endpoints are all methods on a single `public static partial class EndpointMapping`, split across ~10 files in `backend/Kasanie.Api/Endpoints/` (AuthEndpoints.cs, PublicDiscoveryEndpoints.cs, SchoolEndpoints.cs, AdminEndpoints.cs, PlayerEndpoints.cs, ParentEndpoints.cs, CoachEndpoints.cs, CoachCommandCenterEndpoints.cs, TeamTrainingEndpoints.cs, EndpointMapping.cs).

Handlers are `private static async Task<IResult>` methods referenced by `MapGet/MapPost` in a per-file `MapXxx(this IEndpointRouteBuilder app)`. No MediatR, no handler classes, no service layer for endpoint logic — DI services (`AppDbContext`, `IAuditService`, `IConfiguration`, `ITransactionalEmailSender`, `ILoggerFactory`) are injected directly as handler parameters.

**Shared helpers, non-obvious location:** `TrySendAsync(emailSender, loggerFactory, recipient, subject, html, text)` and `BuildUrl(configuration, path)` are `private static` on the partial class, physically defined in `AuthEndpoints.cs` (~lines 234-247) but used from SchoolEndpoints, AdminEndpoints and PublicDiscoveryEndpoints. Because it is one partial class, any endpoint file can call them with no import. `TrySendAsync` swallows+logs send exceptions on purpose (email failure must not fail the request). Branded templates are static methods on `Kasanie.Api.Infrastructure.EmailTemplates`.

**How to apply:** New transactional email from any endpoint = add a template method to `EmailTemplates`, call `EmailTemplates.X(...)` + `TrySendAsync` after `SaveChangesAsync`. Do not create a notification service/class unless a genuine multi-recipient/retry concern appears — the codebase norm is inline send-after-save with local helpers.
