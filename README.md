# Bookable — resource booking

A web app where employees book meeting rooms, pool cars and catering, and admins manage
users, resources, booking constraints, and see how it is all being used.

**Live app:** _link in the submission email_ · **Demo access:** open the app and pick a
user — *Priya Sharma* is the admin; everyone else is a regular user. (Demo-mode login is
a deliberate decision, not a shortcut — see [Decisions](#decisions-and-the-roads-not-taken).)

Built for the Aries / Add-On Products engineering case study.

## Run locally

```bash
docker compose up -d        # Postgres 17 on localhost:5455
cd Api && dotnet run        # API on http://localhost:5214 — migrates + seeds demo data
cd client && npx ng serve   # SPA on http://localhost:4200 (proxies /api and /hubs)
```

Needs .NET 10, Node 24+, Docker. `OPENAI_API_KEY` in the environment enables the two AI
features; without it they degrade gracefully (503 from the API, hidden/explained in the UI).

Tests: `dotnet test` (unit + integration — integration spins up a real Postgres via
Testcontainers, so Docker must be running) and `cd client && npx ng test`.

## What it does, and why each piece earns its place

| Feature | Value to the company |
| --- | --- |
| Browse + book rooms, cars and catering on a week availability grid | The core job: removes the email/spreadsheet round-trip for shared resources |
| Typed booking constraints per resource (duration, notice, advance window, opening hours, per-user limits) | Facilities encode policy once instead of policing it; users get told the rule, not just "no" |
| Double-booking prevention at two layers (app + database) | Trust: the one failure booking software is never forgiven |
| Live availability + in-app notifications (SignalR) | Two people looking at one room see the same truth; a cancelled booking tells its owner immediately |
| Utilisation dashboard + usage history | Makes the data actionable: which rooms are wasted, when peak load hits, who the heavy users are |
| NL booking assistant (*"book a room for 6 tomorrow 2pm"*) | Fastest path from intent to booking — and every AI proposal passes the same rules as the form |
| AI utilisation digest for admins | The numbers, narrated — what a facilities manager would tell their boss |
| One-click demo login, realistic seeded fortnight | Reviewers see a living product in ten seconds, with real role enforcement behind it |

## Architecture

```
client/  Angular 22 — standalone components, signals, Tailwind v4
         features → ApiClient (abstract) → HTTP | in-memory mock (one flag)
Api/     .NET 10 — Controllers → Services → EF Core → PostgreSQL 17
         Domain/ (entities + BookingRules)   Ai/ (assistant + insights)
         Hubs/ (SignalR)                     Data/ (DbContext, migrations, seed)
```

One container in production: the Angular build is served from ASP.NET's `wwwroot`, so
there is one URL, no CORS, and cookie auth just works. The REST + SignalR contract is
frozen in [`docs/contracts.md`](docs/contracts.md) and mirrored across the C# DTOs and
the TypeScript models.

### Decisions, and the roads not taken

- **No MediatR, no handler classes, no repository wrappers, no custom Unit of Work.**
  The EF Core `DbContext` is the unit of work. At this size those layers are ceremony:
  they would multiply files, not value. Controllers stay thin and services are the only
  data surface (`AsNoTracking` on every read).
- **Two projects, layers as folders** with a one-way dependency rule — the same
  boundaries project-per-layer would give, without the solution sprawl.
- **PostgreSQL, not SQLite** — chosen by the double-booking design below; SQLite cannot
  express the constraint that makes the guarantee real.
- **Demo "login as" picker instead of passwords/SSO.** Reviewers click straight in, but
  every endpoint still enforces roles server-side from the auth cookie — a user hitting
  an admin endpoint gets a 403 (integration-tested). Swapping the picker for real
  credentials touches one controller and one component.
- **Hand-rolled availability grid, no calendar library.** The grid *is* the product
  here; owning it keeps the bundle small and every line explainable.
- **Signals + standalone components** on the front; strict TypeScript with no `any`;
  every component in separate `.ts`/`.html`/`.scss` files.
- **Errors are RFC 7807 ProblemDetails everywhere**, with machine-readable rule codes in
  a `violations` extension (409 = collision, 422 = other rule failures), so the client
  renders the server's reasons instead of inventing its own.

## The booking rules engine

Every booking mutation — the form, the AI assistant, anything future — passes through
one choke point: `BookingRules.Validate(context)`. It is pure (no EF, no clock, no I/O —
everything arrives in the context), returns **all** violations rather than the first,
and each constraint type is one testable rule: max duration, max advance, minimum
notice, opening hours (evaluated in Europe/London, DST-tested), max concurrent and max
upcoming per user, plus the capacity model.

Resources are heterogeneous under one model: rooms and cars are *exclusive*
(capacity 1), catering is *pooled* (40 portions per window; bookings carry a quantity
and the rule is *sum of overlapping quantities ≤ capacity*). Back-to-back bookings are
allowed; cancelled bookings never count.

## Double-booking: belt and braces

1. **Application layer** — booking creation runs in a transaction holding a Postgres
   **advisory lock per resource** (`pg_advisory_xact_lock`), making check-then-insert
   atomic against concurrent requests without table locks. The loser gets a friendly
   409 with rule codes.
2. **Database backstop** — a GiST **exclusion constraint** on confirmed, exclusive
   bookings: `EXCLUDE USING gist (ResourceId WITH =, tstzrange(Start, End) WITH &&)`.
   Even a bug that bypasses the service layer cannot commit an overlap; the handler maps
   the violation to the same 409 and logs it loudly, because it firing means a code path
   skipped the lock.

The integration suite fires two simultaneous requests at one slot against a real
Postgres and asserts exactly one 201 and one 409.

## The AI features (gpt-4.1-nano), boxed in

The model is **never trusted and never required**:

- The **assistant** does exactly one job — turn free text into a `BookingIntent`
  (type / seats / quantity / date / time / duration) through a strict JSON schema. It
  names no resource ids and takes no actions. Deterministic code then ranks candidates
  (smallest sufficient room first) and dry-runs each through `BookingRules`; only
  passing proposals are shown, and confirming one goes through the normal booking path.
  Small models are poor at weekday arithmetic, so the prompt carries a 14-day calendar
  — verified live ("Monday" resolved to the wrong day without it, the right one with it).
- The **insights digest** narrates numbers computed by `StatsService`; it is labelled as
  AI-generated and asked to add nothing the data does not contain.
- The key lives in the `OPENAI_API_KEY` environment variable only. Keyless deployments
  (and CI, by design) get 503s and a UI that says so — covered by an integration test.
  Model calls have timeouts and fail soft to the grid.

## Testing

- **37 unit tests** — every rule in the engine (boundaries: back-to-back adjacency,
  exact-capacity fit, one-unit-over, DST transition day, closed days, malformed
  constraint params disabling only themselves) and the assistant's planner.
- **11 integration tests** on real Postgres via Testcontainers — auth, role enforcement,
  the concurrency race, cancellation + notification, availability, dashboard, AI 503s.
- **8 frontend tests** — the grid slot math (mirroring the server's strict overlap
  semantics) and component creation.
- CI (GitHub Actions) runs format, build, both test suites and lint on every push —
  the same gates the agent had to satisfy during development (see below).

## How it was built

Built AI-assisted, deliberately in the open: the agent (Claude Code) worked under
[`CLAUDE.md`](CLAUDE.md) — a hard constraints file I wrote before the first feature
(no MediatR, all mutations through `BookingRules`, strict TS, tests required per rule,
ProblemDetails everywhere) — with `dotnet format` + both test suites + lint as its
definition of done, enforced again by CI. The method comes out of my open-source work on
[Conductor](https://github.com/shaahink/conductor), an orchestrator for agentic
engineering workflows: constraints and verification gates up front, so the agent cannot
mark its own homework, and the human owns every line that lands. The commit history *is*
the build log — contracts frozen first, then the rule engine test-first, then API,
UI, AI, integration; each stage landed green.

## What I'd do next

- Approval workflows (a constraint type whose violation parks a booking as *pending*).
- Recurring bookings; Outlook/M365 calendar sync at the availability boundary.
- Playwright smoke suite on the deployed URL (book → conflict → cancel).
- Per-user notification preferences and email delivery behind the existing
  `Notification` row.
- Pagination + export on usage history; utilisation targets per resource on the
  dashboard.
