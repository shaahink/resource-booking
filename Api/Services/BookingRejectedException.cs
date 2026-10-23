using Api.Domain;

namespace Api.Services;

/// <summary>
/// Thrown by the booking service when BookingRules reports violations (or the DB
/// exclusion constraint fires). Mapped to an RFC 7807 response by ApiExceptionHandler:
/// 409 when the booking collides with others, 422 for any other rule failure.
/// </summary>
public class BookingRejectedException(IReadOnlyList<RuleViolation> violations) : Exception("Booking rejected")
{
    public IReadOnlyList<RuleViolation> Violations { get; } = violations;

    public bool IsConflict => Violations.Any(v =>
        v.Code is RuleCodes.OverlapConflict or RuleCodes.CapacityExceeded);
}
