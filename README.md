# Bookable — resource booking

Employees book meeting rooms, pool cars and catering; facilities admins set the rules and
see how everything is used.

**Live:** <https://bookable-wpfp.onrender.com>. It's on a free tier, so the first load after
an idle spell takes about a minute. **Log in** by picking a user. *Priya Sharma* is the
admin; everyone else is a regular user.

Built for the Aries / Add-On Products engineering case study. The README has two parts:
**[Product](#part-1--product)** (who it's for, what it does, why) and
**[Engineering](#part-2--engineering)** (how it's built, and the trade-offs).

```bash
docker compose up -d          # Postgres 17 on localhost:5455
cd Api && dotnet run          # API on :5214, migrates and seeds demo data
cd client && npx ng serve     # second terminal: SPA on :4200, proxies /api
```

Needs .NET 10, Node 24+ and Docker. `OPENAI_API_KEY` enables the two AI features; without
it they switch themselves off and say so.

---

# Part 1 — Product

## Who it's for

| | Job to be done |
| --- | --- |
| **Employee** | Find something free that fits, book it in seconds, and know straight away whether the rules allow it |
| **Facilities admin** | Set policy once (hours, notice, limits) instead of policing it, and see which resources are wasted or overloaded |

## What it does

- **Browse and book** rooms, cars and catering on a week grid. The grid shades closed
  hours and states the rules it enforces ("Mon–Fri 08:00–20:00 · max 2 h").
- **Ask in plain English:** *"a room for 6 tomorrow at 2pm"*. The assistant proposes only
  slots that would actually be accepted. When nothing can fit, it says why: *"No room
  seats 30, the largest is Workshop Room (20)."*
- **My bookings**, grouped by day, with Upcoming and Past tabs, type filters, search, and
  one-click *Open in calendar*, *Cancel* and *Book again*.
- **Notifications:** your bookings and an admin's cancellations land in the bell, and
  each one opens the booking it refers to.
- **Admin:** resources with typed booking rules, users and roles, usage history, and a
  utilisation dashboard. The dashboard's rows click through to that resource's or user's
  history, and an AI digest narrates the numbers.

## Product decisions

- **Show the rule before the click, explain it after.** The booking dialog only offers
  durations the resource allows. Anything the form can't know (notice, overlaps,
  per-user limits) comes back with every reason, not just the first.
- **The AI never decides.** It turns text into an intent. Ordinary code picks candidates
  and checks each one against the same rules as the form, so the assistant can't promise
  a slot the system would refuse.
- **History is a record.** A booking that has ended can't be cancelled, so the usage
  numbers can't be rewritten after the fact.
- **Readable links:** `/resources/boardroom`, not a GUID. A slug keeps its value when the
  resource is renamed, so shared links keep working.
- **Demo login on purpose.** A reviewer is in within seconds, and roles are still enforced
  on every endpoint.
- **Desktop-first.** Booking a room is a desk task. The grid scrolls on a phone, but the
  layout isn't tuned for one.

## What I'd build next

1. **Booked isn't used.** Add check-in, and release no-shows automatically, so utilisation
   measures real use.
2. **Outlook / M365 sync** at the availability boundary, since that's where the customers
   already book.
3. **A finder across all resources** ("what's free at 14:00 for 6?") without the assistant.
4. Recurring bookings, approval workflows, and email delivery for notifications.

---

# Part 2 — Engineering

## Architecture

```
client/  Angular 22 — standalone components, signals, Tailwind v4
         features → ApiService (HTTP) · core/settings.ts holds the client's fixed numbers
Api/     .NET 10 — Controllers → Services → EF Core → PostgreSQL 17
         Domain/     entities, BookingRules (the rule engine), RuleCodes, FieldLimits, Slugs
         Services/   bookings, resources, users, stats, notifications
         Ai/         assistant + insights          Contracts/  requests, responses, validators
         Web/        errors, session checks         Data/       DbContext, migrations, seed
```

One container in production: ASP.NET serves the Angular build from `wwwroot`. That means
one URL, no CORS, and cookie auth just works. [`docs/contracts.md`](docs/contracts.md) is
the API contract, mirrored by the C# and TypeScript models.

## Engineering decisions

- **Boring layers, on purpose.** No MediatR, no repository wrappers, no custom unit of
  work; the `DbContext` already is one. Controllers are thin, services are the only data
  access, and reads use `AsNoTracking`.
- **One rule engine, one path into it.** `BookingRules.Validate` is pure: no EF, clock or
  I/O. It returns *all* violations, and each constraint type is one tested rule. The
  booking form and the assistant both reach it through `BookingService.CheckRulesAsync`.
- **Double booking is prevented twice.** A per-resource Postgres advisory lock makes
  check-then-insert atomic. Under it, a GiST exclusion constraint on confirmed exclusive
  bookings (`tstzrange(Start, End) WITH &&`) means an overlap can't be committed even by a
  code path that skips the lock. Pooled catering is capped at the *peak* number of units
  in use at any instant, not the sum.
- **Errors are ProblemDetails.** Rule codes travel in a `violations` extension: 409 when
  any violation is a collision, 422 for every other rule failure. The client renders the
  server's reasons rather than inventing its own.
- **The AI is boxed in.** The model only turns text into an intent through a strict JSON
  schema. It sees a 14-day calendar so it never does weekday arithmetic, and no employee
  names are sent to it. Calls have timeouts and fail with an honest 502. It runs on
  gpt-6-luna because OpenAI retired the gpt-4.1-nano named in the brief during the
  exercise; the model is one setting.
- **No magic numbers.** `FieldLimits` is shared by the EF mapping and the validators.
  `BookingRules.SlotMinutes` is the one slot length. Operational settings (model, timeout,
  digest window, digest cache) live in the `OpenAI` section of `appsettings.json`. The
  client's numbers live in `core/settings.ts`, each noting the server constant it mirrors.
- **The availability grid is hand-built** (`grid.ts`, pure and tested). The grid *is* the
  product here, and owning it keeps every line explainable.
- **No realtime channel.** SignalR was tried and removed: the grid refetches after every
  change and notifications are fetched. It's one less stateful piece to run.

## Testing

- **94 unit tests** covering every rule boundary (back-to-back bookings, exact capacity,
  peak capacity, the DST day, past and backdated slots, closed days), the slug rule, the
  assistant's planner and `DATABASE_URL` parsing.
- **31 integration tests** on real Postgres via Testcontainers: auth and roles, session
  revalidation, both concurrency races (one 201, the rest 409), notifications,
  cancellation rules, slugs, validation, the dashboard, AI 503 vs 502, and rate limiting.
- **28 frontend tests:** grid slot maths, closed cells, the durations the dialog offers,
  My bookings grouping, the constraint editor round trip, and the 401 interceptor.
- **CI** runs format, build, both suites and lint on every push. Warnings are errors under
  the recommended, Meziantou and Sonar analyzers.

## Deploy

One Docker image plus managed Postgres. A Render Blueprint is in
[`render.yaml`](render.yaml). The app migrates and seeds on boot; that assumes a single
replica, and the assumption is deliberate.

- **Database:** `DATABASE_URL` (`postgres://…`, converted in `Api/Data/DatabaseUrl.cs`) or
  `ConnectionStrings__Default`.
- **AI:** optional `OPENAI_API_KEY`.
- **Health:** `/health` only answers when the database does.
- **Demo:** *Reset demo data* on the dashboard (or `POST /api/admin/demo/reset`) restores
  the seeded fortnight.

## How it was built

I built it AI-assisted, in the open. The coding agent worked under
[`CLAUDE.md`](CLAUDE.md), a constraints file I wrote before the first feature. Format,
both test suites and lint were its definition of done, and CI enforces them again. The
method comes from my open-source orchestrator
[Conductor](https://github.com/shaahink/conductor): set constraints and verification
gates up front, so the agent can't mark its own homework. The commit history is the build
log.
