# API contract (frozen at S1)

Source of truth triple: `Api/Contracts/Dtos.cs` ↔ `client/src/app/core/models.ts` ↔ this
file. Swagger (`/swagger` in dev) renders the live OpenAPI from the C# side. Enums travel
as strings. All times are UTC ISO 8601; the client renders Europe/London local time.

## Endpoints

| Method & path | Auth | Request | Response |
| --- | --- | --- | --- |
| `GET /api/auth/users` | — | | `UserDto[]` (login picker) |
| `POST /api/auth/login` | — | `{ userId }` | `UserDto` + auth cookie |
| `POST /api/auth/logout` | user | | 204 |
| `GET /api/auth/me` | user | | `UserDto` |
| `GET /api/resources?type=` | user | | `ResourceDto[]` (active only) |
| `GET /api/resources/{id}` | user | | `ResourceDto` |
| `GET /api/resources/{id}/availability?from=&to=` | user | | `AvailabilityDto` — raw confirmed bookings in range; the client computes the grid |
| `POST /api/bookings` | user | `CreateBookingRequest` | 201 `BookingDto` · 409/422 rule problem |
| `DELETE /api/bookings/{id}` | owner or admin | | 204; notifies the owner if an admin cancelled |
| `GET /api/bookings/mine` | user | | `BookingDto[]` |
| `GET /api/notifications` | user | | `NotificationDto[]` (newest first) |
| `POST /api/notifications/{id}/read` | user | | 204 |
| `POST /api/admin/resources` | admin | `SaveResourceRequest` | 201 `ResourceDto` |
| `PUT /api/admin/resources/{id}` | admin | `SaveResourceRequest` | `ResourceDto` (constraints replaced wholesale) |
| `GET /api/admin/resources` | admin | | `ResourceDto[]` including inactive |
| `GET /api/admin/users` | admin | | `UserDto[]` |
| `POST /api/admin/users` | admin | `SaveUserRequest` | 201 `UserDto` |
| `PUT /api/admin/users/{id}` | admin | `SaveUserRequest` | `UserDto` |
| `GET /api/admin/bookings?resourceId=&userId=&from=&to=` | admin | | `BookingDto[]` (history, newest first) |
| `GET /api/admin/stats/dashboard?from=&to=` | admin | | `DashboardDto` |
| `GET /api/admin/stats/summary` | admin | | `InsightsSummaryDto` (AI digest; 503 when no key) |
| `POST /api/assistant/parse-booking` | user | `{ text }` | `AssistantParseResponse` (503 when no key) |

SignalR hub `/hubs/bookings` (cookie-authenticated):

- client → server: `WatchResource(resourceId)`, `UnwatchResource(resourceId)`
- server → group `resource:{id}`: `bookingCreated(resourceId, bookingId)`,
  `bookingCancelled(resourceId, bookingId)`
- server → user: `notification(message)`

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

409 = conflict with other bookings (`overlap_conflict`, `capacity_exceeded`);
422 = any other rule failure; 400 = shape validation (FluentValidation).

## Rule codes

`invalid_range` · `quantity_invalid` · `inactive_resource` · `overlap_conflict` ·
`capacity_exceeded` · `max_duration_exceeded` · `too_far_in_advance` ·
`insufficient_notice` · `outside_opening_hours` · `max_concurrent_per_user` ·
`max_active_bookings`

## Constraint parameter shapes (`ResourceConstraint.ParamsJson`)

| ConstraintType | params | Meaning |
| --- | --- | --- |
| `MaxDurationMinutes` | `{"minutes":120}` | Booking length ≤ minutes |
| `MaxAdvanceDays` | `{"days":60}` | Start ≤ now + days |
| `MinNoticeMinutes` | `{"minutes":60}` | Start ≥ now + minutes |
| `OpeningHours` | `{"days":{"mon":[["08:00","20:00"]],…}}` | Booking fits inside a window on its weekday (Europe/London); absent day = closed |
| `MaxConcurrentPerUser` | `{"count":1}` | User's overlapping confirmed bookings on this resource < count at any instant |
| `MaxActiveBookingsPerUser` | `{"count":2}` | User's upcoming confirmed bookings on this resource ≤ count |

## Semantics the implementation must honour

- Capacity model: rooms/cars have `capacity = 1` (exclusive); catering is pooled —
  sum of overlapping confirmed quantities ≤ capacity. `Booking.IsExclusive` is
  denormalised from `capacity == 1` at creation and drives the DB exclusion constraint.
- Adjacency allowed: a booking may start exactly when another ends.
- Cancelled bookings never count toward overlap, capacity or user limits.
- `quantity` must be ≥ 1, and 1 for exclusive resources.
