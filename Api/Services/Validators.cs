using System.Text.Json;
using Api.Contracts;
using FluentValidation;

namespace Api.Services;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class SaveResourceRequestValidator : AbstractValidator<SaveResourceRequest>
{
    public SaveResourceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 10_000);
        RuleFor(x => x.Seats).InclusiveBetween(1, 10_000).When(x => x.Seats is not null);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleForEach(x => x.Constraints).ChildRules(constraint =>
        {
            constraint.RuleFor(c => c.ParamsJson)
                .NotEmpty()
                .Must(BeValidJson).WithMessage("Constraint parameters must be valid JSON.");
        });
    }

    private static bool BeValidJson(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class SaveUserRequestValidator : AbstractValidator<SaveUserRequest>
{
    public SaveUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public class AssistantParseRequestValidator : AbstractValidator<AssistantParseRequest>
{
    public AssistantParseRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
    }
}
