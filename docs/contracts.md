# API contract (frozen at S1)

Source of truth triple: `Api/Contracts/Requests.cs` + `Api/Contracts/Responses.cs` ↔
`client/src/app/core/models.ts` ↔ this file. Swagger (`/swagger` in dev) renders the
live OpenAPI from the C# side. Enums travel as strings. All times are UTC ISO 8601;
the client renders the viewer's local time. Request types end in `Request`, response
types in `Response`; entities carry a `createdOn` timestamp surfaced in the UI.

## Endpoints

| Method & path | Auth | Request | Response |
| --- | --- | --- | --- |
| `GET /api/auth/users` | — | | `LoginOptionResponse[]` — `{ id, name, role }`, active users only, no emails (anonymous login picker) |
| `POST /api/auth/login` | — | `{ userId }` | `UserResponse` + auth cookie; 401 for a disabled user |
| `POST /api/auth/logout` | user | | 204 |
| `GET /api/auth/me` | user | | `UserResponse` |
| `GET /api/resources?type=` | user | | `ResourceResponse[]` (active only) |
| `GET /api/resources/{id}` | user | | `ResourceResponse` |
| `GET /api/resources/{id}/availability?from=&to=` | user | | `AvailabilityResponse` — raw confirmed bookings in range; the client computes the grid. 400 unless the range is valid (below) |
| `POST /api/bookings` | user | `CreateBookingRequest` | 201 `BookingResponse` · 409/422 rule problem |
| `DELETE /api/bookings/{id}` | owner or admin | | 204; notifies the owner if an admin cancelled |
| `GET /api/bookings/mine` | user | | `BookingResponse[]` |
| `GET /api/notifications` | user | | `NotificationResponse[]` (newest first) |
| `POST /api/notifications/{id}/read` | user | | 204 |
| `POST /api/admin/resources` | admin | `SaveResourceRequest` | 201 `ResourceResponse` |
| `PUT /api/admin/resources/{id}` | admin | `SaveResourceRequest` | `ResourceResponse` (constraints replaced wholesale) |
| `GET /api/admin/resources` | admin | | `ResourceResponse[]` including inactive |
| `GET /api/admin/users` | admin | | `UserResponse[]` including disabled |
| `POST /api/admin/users` | admin | `SaveUserRequest` | 201 `UserResponse` · 409 duplicate email |
| `PUT /api/admin/users/{id}` | admin | `SaveUserRequest` | `UserResponse` · 409 duplicate email |
| `GET /api/admin/bookings?resourceId=&userId=&from=&to=` | admin | | `BookingResponse[]` (history, newest first, capped at 500) |
| `GET /api/admin/stats/dashboard?from=&to=` | admin | | `DashboardResponse`. 400 unless the range is valid (below) |
| `GET /api/admin/stats/summary` | admin | | `InsightsSummaryResponse` (AI digest, cached 10 min; 503 when no key; 502 when the model provider fails; 429 over the AI rate limit) |
| `POST /api/admin/demo/reset` | admin | | 204 — wipes every table and reseeds the demo data |
| `POST /api/assistant/parse-booking` | user | `{ text }` | `AssistantParseResponse` (503 when no key; 502 when the model provider fails; 429 over the AI rate limit) |

A `from`/`to` range (`DateRangeQuery`) is half-open `[from, to)`: both are required,
`to > from`, at most 366 days apart, and any UTC offset is accepted.

Disabling a user (`SaveUserRequest.isActive = false`) keeps their history but removes
them from the login picker and blocks sign-in; existing bookings are untouched. Every
request re-checks the session against the user row: a disabled or deleted user's cookie,
or one whose role has changed since sign-in, gets 401 and is signed out.

The AI endpoints share a rate limit of 10 requests a minute per client address. An
unknown `/api/*` route is a ProblemDetails 404, never the SPA's `index.html`.

`InsightsSummaryResponse` is structured for formatting: `{ headline, highlights[],
suggestion, generatedAt }`.

## Error shape

RFC 7807 ProblemDetails. Rule failures add a `violations` extension:

```json
{
  "title": "Booking rejected",
  "status": 409,
  "violations": [
    { "code": "overlap_conflict", "message": "This slot is already booked." }
  ]
}
```

409 = conflict with other bookings (`overlap_conflict`, `capacity_exceeded`) or a
uniqueness conflict (duplicate user email, no `violations` extension);
422 = any other rule failure; 400 = shape validation (FluentValidation — including
`end > start`, bookings ≤ 14 days, and constraint params matching their type's shape)
or a missing value-type field: `isActive`, `quantity`, `start` and the like are required,
never defaulted.

## Rule codes

`invalid_range` · `in_the_past` · `quantity_invalid` · `inactive_resource` ·
`overlap_conflict` · `capacity_exceeded` · `max_duration_exceeded` ·
`too_far_in_advance` · `insufficient_notice` · `outside_opening_hours` ·
`max_concurrent_per_user` · `max_active_bookings`

`in_the_past` triggers on `end <= now`, or on a start before the current half-hour
slot began: the slot that is running is still bookable — matching the grid's definition
of a past cell — but a booking cannot reach back any further.

## Constraint parameter shapes (`ResourceConstraint.ParamsJson`)

| ConstraintType | params | Meaning |
| --- | --- | --- |
| `MaxDurationMinutes` | `{"minutes":120}` | Booking length ≤ minutes (1–525600) |
| `MaxAdvanceDays` | `{"days":60}` | Start ≤ now + days (1–3650) |
| `MinNoticeMinutes` | `{"minutes":60}` | Start ≥ now + minutes (1–525600) |
| `OpeningHours` | `{"days":{"mon":[["08:00","20:00"]],…}}` | Booking fits inside a window on its weekday (Europe/London); absent day = closed. Keys `mon`…`sun`, any case; times strict `HH:mm`, `"24:00"` = midnight, close > open |
| `MaxConcurrentPerUser` | `{"count":1}` | User's overlapping confirmed bookings on this resource < count at any instant (1–1000) |
| `MaxActiveBookingsPerUser` | `{"count":2}` | User's upcoming confirmed bookings on this resource ≤ count (1–1000) |

`Domain/ConstraintParams.cs` is the one reader of these shapes. The admin API rejects
anything it cannot read on save (400); the rule engine skips it and stats treat it as
absent, so a legacy malformed row disables one rule rather than taking the resource
offline. A missing or null number is malformed — it never reads as 0.

## Semantics the implementation must honour

- Capacity model: rooms/cars have `capacity = 1` (exclusive); catering is pooled —
  at every instant of the booking, the confirmed quantities in use plus the request
  ≤ capacity (the peak, not the sum of everything that touches the window). `Booking.IsExclusive` is
  denormalised from `capacity == 1` at creation and drives the DB exclusion constraint.
  Capacity edits apply to new bookings; existing rows keep the flag they were created with.
- Adjacency allowed: a booking may start exactly when another ends.
- Cancelled bookings never count toward overlap, capacity or user limits.
- `quantity` must be ≥ 1, and 1 for exclusive resources.
