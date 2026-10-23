using System.Text.Json;
using Api.Contracts;
using Api.Domain;
using FluentValidation;

namespace Api.Contracts;

// Request *shape* only — business rules live in BookingRules.

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    /// <summary>Sanity cap, not policy: pool cars legitimately go multi-day, nothing sane books a fortnight.
    /// Per-resource duration policy is the MaxDurationMinutes constraint.</summary>
    private const int MaxBookingDays = 14;

    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.End)
            .GreaterThan(x => x.Start).WithMessage("End must be after start.")
            .Must((request, end) => end - request.Start <= TimeSpan.FromDays(MaxBookingDays))
            .WithMessage($"Bookings are limited to {MaxBookingDays} days.");
    }
}

public sealed class SaveResourceRequestValidator : AbstractValidator<SaveResourceRequest>
{
    public SaveResourceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 10_000);
        RuleFor(x => x.Seats).InclusiveBetween(1, 10_000).When(x => x.Seats is not null);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleForEach(x => x.Constraints)
            .Must(MatchTheShapeForTheirType)
            .WithMessage("Constraint parameters do not match the constraint type — docs/contracts.md lists the shapes.");
    }

    /// <summary>The typed shapes from docs/contracts.md, enforced at the boundary. A wrong-shaped
    /// constraint would otherwise silently neuter a rule (the engine skips what it cannot parse).</summary>
    private static bool MatchTheShapeForTheirType(SaveConstraintRequest constraint)
    {
        try
        {
            using var doc = JsonDocument.Parse(constraint.ParamsJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            return constraint.Type switch
            {
                ConstraintType.MaxDurationMinutes or ConstraintType.MinNoticeMinutes => HasPositiveInt(root, "minutes"),
                ConstraintType.MaxAdvanceDays => HasPositiveInt(root, "days"),
                ConstraintType.MaxConcurrentPerUser or ConstraintType.MaxActiveBookingsPerUser => HasPositiveInt(root, "count"),
                ConstraintType.OpeningHours => root.TryGetProperty("days", out var days) && days.ValueKind == JsonValueKind.Object,
                _ => false,
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasPositiveInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
        && number >= 1;
}

public sealed class SaveUserRequestValidator : AbstractValidator<SaveUserRequest>
{
    public SaveUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public sealed class AssistantParseRequestValidator : AbstractValidator<AssistantParseRequest>
{
    public AssistantParseRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
    }
}
