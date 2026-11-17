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
        RuleFor(x => x.Quantity).InclusiveBetween(1, FieldLimits.MaxQuantity);
        RuleFor(x => x.Notes).MaximumLength(FieldLimits.Notes);
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
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLimits.Name);
        RuleFor(x => x.Capacity).InclusiveBetween(1, FieldLimits.MaxCapacity);
        RuleFor(x => x.Seats).InclusiveBetween(1, FieldLimits.MaxCapacity).When(x => x.Seats is not null);
        RuleFor(x => x.Location).MaximumLength(FieldLimits.Location);
        RuleFor(x => x.Description).MaximumLength(FieldLimits.Description);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Constraints).NotNull();
        RuleForEach(x => x.Constraints)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(c => Enum.IsDefined(c.Type) && ConstraintParams.IsValid(c.Type, c.ParamsJson))
            .WithMessage("Constraint parameters do not match the constraint type — docs/contracts.md lists the shapes.");
    }
}

public sealed class DateRangeQueryValidator : AbstractValidator<DateRangeQuery>
{
    private const int MaxRangeDays = 366;

    public DateRangeQueryValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To)
            .NotEmpty()
            .GreaterThan(x => x.From).WithMessage("'to' must be after 'from'.")
            .Must((range, to) => to - range.From <= TimeSpan.FromDays(MaxRangeDays))
            .WithMessage($"A range covers at most {MaxRangeDays} days.");
    }
}

public sealed class SaveUserRequestValidator : AbstractValidator<SaveUserRequest>
{
    public SaveUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLimits.Name);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(FieldLimits.Email);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class AssistantParseRequestValidator : AbstractValidator<AssistantParseRequest>
{
    public AssistantParseRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(FieldLimits.AssistantText);
    }
}
