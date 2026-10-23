namespace Api.Domain;

/// <summary>
/// Machine-readable rule codes carried in ProblemDetails responses.
/// Frozen as part of the API contract (docs/contracts.md) — the frontend maps these to messages too.
/// </summary>
public static class RuleCodes
{
    public const string InvalidRange = "invalid_range";
    public const string QuantityInvalid = "quantity_invalid";
    public const string InactiveResource = "inactive_resource";
    public const string OverlapConflict = "overlap_conflict";
    public const string CapacityExceeded = "capacity_exceeded";
    public const string MaxDurationExceeded = "max_duration_exceeded";
    public const string TooFarInAdvance = "too_far_in_advance";
    public const string InsufficientNotice = "insufficient_notice";
    public const string OutsideOpeningHours = "outside_opening_hours";
    public const string MaxConcurrentPerUser = "max_concurrent_per_user";
    public const string MaxActiveBookings = "max_active_bookings";
}

public record RuleViolation(string Code, string Message);

/// <summary>
/// Everything the rules need, gathered by the service layer. The rules themselves are pure:
/// no EF, no clock, no I/O — fully unit-testable.
/// </summary>
public record BookingContext(
    Resource Resource,
    IReadOnlyList<ResourceConstraint> Constraints,
    User User,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Quantity,
    IReadOnlyList<Booking> OverlappingConfirmedBookings,
    IReadOnlyList<Booking> UsersUpcomingConfirmedBookings,
    DateTimeOffset Now);

/// <summary>
/// THE single choke point for booking validation. Every booking mutation — the form,
/// the AI assistant, anything future — passes through Validate. Returns ALL violations, never just the first.
/// </summary>
public interface IBookingRules
{
    IReadOnlyList<RuleViolation> Validate(BookingContext context);
}
