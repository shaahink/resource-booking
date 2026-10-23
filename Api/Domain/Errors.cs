namespace Api.Domain;

// The exceptions services throw to signal an outcome the client must see.
// Web/ApiExceptionHandler maps each one to its ProblemDetails status.

/// <summary>
/// BookingRules reported violations, or the DB exclusion constraint fired.
/// 409 when the booking collides with others, 422 for any other rule failure.
/// </summary>
public sealed class BookingRejectedException(IReadOnlyList<RuleViolation> violations) : Exception("Booking rejected")
{
    public IReadOnlyList<RuleViolation> Violations { get; } = violations;

    public bool IsConflict => Violations.Any(v =>
        v.Code is RuleCodes.OverlapConflict or RuleCodes.CapacityExceeded);
}

/// <summary>A uniqueness conflict outside the booking rules (e.g. duplicate email). 409.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>The model provider failed: an outage, a retired model, a timeout. 502 — the
/// feature exists but is down, which is not the user's fault and not "not configured".</summary>
public sealed class AiUnavailableException(Exception inner)
    : Exception("The AI model is unavailable right now. Booking from the grid works as normal.", inner);

/// <summary>The AI feature is absent on this deployment, not broken. 503.</summary>
public sealed class AiNotConfiguredException() : Exception("AI features are not configured on this deployment.");
