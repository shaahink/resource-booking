namespace Api.Domain;

/// <summary>
/// Machine-readable rule codes carried in ProblemDetails responses.
/// Frozen as part of the API contract (docs/contracts.md) — the frontend maps these to messages too.
/// </summary>
public static class RuleCodes
{
    public const string InvalidRange = "invalid_range";
    public const string InThePast = "in_the_past";
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
