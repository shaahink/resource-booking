# Bookable — resource booking case study

A web app where employees book meeting rooms, pool cars and catering; admins manage
users, resources, constraints and see utilisation. .NET 10 API + Angular 22 + PostgreSQL.

## Commands

```bash
docker compose up -d                 # Postgres on localhost:5455
cd Api && dotnet run                 # API on http://localhost:5214 (migrates + seeds)
cd client && npx ng serve            # SPA on http://localhost:4200 (proxies /api)

dotnet build ResourceBooking.sln
dotnet test                          # unit + integration (integration needs Docker)
dotnet format ResourceBooking.sln --verify-no-changes
cd client && npx ng lint && npx ng test && npx ng build
```

## Definition of done — every task, no exceptions

A task is complete only when **all** of these are green. CI runs the same gates.

1. `dotnet format --verify-no-changes` and `dotnet build` (warnings are errors)
2. `dotnet test` — including the tests the task itself was required to add
3. `cd client && npx ng lint && npx ng test && npx ng build`

## Architecture rules (hard constraints, not suggestions)

### Backend

- **Two projects only**: `Api` and `Api.Tests`. Layers are folders (`Domain/`, `Data/`,
  `Services/`, `Controllers/`, `Ai/`, `Contracts/`, `Web/`) with a one-way dependency rule:
  Controllers → Services → Data/Domain. Domain references nothing (BCL only).
- **File layout — one convention:** an interface lives in its implementation's file.
  `Services/` holds business services only; HTTP plumbing (exception handler, validation
  filter, health check, session validation, `CurrentUser`) lives in `Web/`; request
  validators sit in `Contracts/` beside the records they validate; the outcome exceptions
  are in `Domain/Errors.cs`. Classes are `sealed` unless built for inheritance.
- **Analyzers are part of the build:** `latest-recommended` + Meziantou + Sonar, warnings
  as errors, code style from the root `.editorconfig`. Every rule switched off there says
  why; fix a finding rather than suppress it, and add a reason if you must.
- **No realtime channel.** SignalR was removed deliberately (README § Decisions): the
  grid refetches around mutations, notifications are fetched. Do not reintroduce it.
- **No MediatR, no handler classes, no repository wrappers, no custom Unit of Work.**
  The EF Core `DbContext` *is* the unit of work. This is a decision; do not "improve" it.
- Controllers are thin: bind, authorize, call one service method, map to a response.
  **Controllers never touch `AppDbContext` or `IQueryable`** — services are the only
  data surface (exception: `AuthController`'s read of users, a deliberate demo-mode cut).
- `AsNoTracking()` on every read query.
- 🔴 **Every booking mutation goes through `IBookingRules.Validate`** — the form, the AI
  assistant, any future path. No rule ever checked anywhere else. Return ALL violations,
  never just the first.
- Rules are pure: no EF, no clock reads, no I/O inside `BookingRules` — everything comes
  in via `BookingContext`. The service layer gathers data and passes `DateTimeOffset.UtcNow`.
- 🔴 **`Domain/ConstraintParams` is the only reader of `ParamsJson`.** The validator, the
  rule engine and stats all ask it; never deserialize constraint params anywhere else.
- Capacity and per-user concurrency are measured at the **peak** instant, never summed
  across the window.
- Booking writes: run inside a transaction holding a **per-resource Postgres advisory
  lock** (`pg_advisory_xact_lock`), re-check availability inside it, then save. The DB
  exclusion constraint `no_double_booking` is the backstop — treat a 23P01 as a 409.
- Errors: RFC 7807 `ProblemDetails` everywhere; rule failures use the `violations`
  extension with codes from `RuleCodes` (409 for conflicts, 422 for other rule failures).
- All times UTC (`DateTimeOffset`/`timestamptz`); the client renders local time. Npgsql
  rejects a non-UTC offset, so every service normalises incoming times with
  `ToUniversalTime()` at its entry.
- FluentValidation for request *shape* only; business rules live in `BookingRules`.
  Value-type request fields are `[JsonRequired]` — a missing field is a 400, never a default.
- Serilog structured logging; no `Console.WriteLine`.
- The OpenAI key comes from env `OPENAI_API_KEY` only. **Never in code, config files or
  the repo.** No key present → AI endpoints return 503 with a clear message and the UI
  hides the features. Model output is never trusted: the assistant produces a *query*,
  whose proposals must pass the availability check + `BookingRules` before being shown.

### Frontend

- Angular 22, standalone components, **signals** for component state. No NgModules.
- **Strict TypeScript, no `any`** (write a precise type or `unknown` + narrowing).
- Every component: separate `.ts` / `.html` / `.scss` files. Tailwind-first styling;
  the `.scss` file is for what utilities can't express (start it with
  `@reference "tailwindcss";` if it uses `@apply`).
- Features talk to data ONLY through the `ApiClient` abstract class. Never inject
  `HttpClient` in a feature. `core/config.ts` `USE_MOCKS` picks mock vs real.
- Keep `MockApiService` behaviour in sync with the real API when contracts change.
- The availability grid is hand-rolled (CSS grid) — **no calendar library**.
- No new runtime dependencies without updating this file's Decisions section.

### Tests

- Every `BookingRules` rule lands WITH its unit tests (happy, violation, boundary).
  Edge cases that must exist: back-to-back adjacency is allowed (end == next start),
  cancelled bookings never count, quantity sums at exact capacity pass, one unit over
  fails, DST boundary day, opening-hours window edges.
- Integration tests use `ApiFactory` (real Postgres via Testcontainers). The concurrency
  test — two parallel creates for one exclusive slot, exactly one 201 and one 409 — must
  stay green.
- Frontend: component logic tests for the booking dialog and grid slot math.

### Contract discipline

`Api/Contracts/Requests.cs` + `Responses.cs` ↔ `client/src/app/core/models.ts` ↔ `docs/contracts.md` are one
contract. A change to any of the three updates all three in the same commit, plus
`MockApiService`.

### Git

- Conventional commits (`feat:`, `fix:`, `test:`, `chore:`, `docs:`).
- Never commit: `.env`, keys, `node_modules`, `bin/`, `obj/`, `dist/`.

## Decisions already made (do not relitigate)

PostgreSQL (exclusion constraint is the double-booking backstop) · cookie auth with a
demo login-as picker (roles still enforced server-side) · one container serving the SPA
from wwwroot (no CORS) · no realtime channel (SignalR removed) · gpt-4.1-nano for both
AI features · Tailwind v4 via PostCSS · no environment-file machinery, one `USE_MOCKS`
flag · data-protection keys in Postgres (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`)
so auth cookies survive restarts and spin-downs.
