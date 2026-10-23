using Api.Domain;

namespace Api.Services;

/// <summary>
/// Stub so the skeleton compiles and wires. Task B1 replaces the body with the real
/// rule engine, test-first — one method per ConstraintType plus overlap/capacity math.
/// </summary>
public class BookingRulesService : IBookingRules
{
    public IReadOnlyList<RuleViolation> Validate(BookingContext context)
    {
        // TODO(B1): implement — see docs/contracts.md § Rule codes and CLAUDE.md.
        var violations = new List<RuleViolation>();

        if (context.End <= context.Start)
            violations.Add(new RuleViolation(RuleCodes.InvalidRange, "End must be after start."));

        return violations;
    }
}
