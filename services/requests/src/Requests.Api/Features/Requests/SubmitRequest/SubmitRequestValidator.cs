using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.ValueObjects;
using FluentValidation;

namespace BloodLink.Requests.Api.Features.Requests.SubmitRequest;

public class SubmitRequestValidator : AbstractValidator<SubmitRequestCommand>
{
    public SubmitRequestValidator()
    {
        RuleFor(x => x.BloodType)
            .NotEmpty()
            .Must(code => BloodType.TryParse(code, out _))
            .WithMessage(
                "BloodType must be one of the 8 valid standard blood type codes (e.g. 'O-', 'A+')."
            );

        RuleFor(x => x.Component)
            .NotEmpty()
            .Must(c => Enum.TryParse<BloodComponent>(c, ignoreCase: true, out _))
            .WithMessage("Component must be either 'RedCells' or 'Platelets'.");

        RuleFor(x => x.UnitsNeeded)
            .InclusiveBetween(1, 20)
            .WithMessage("UnitsNeeded must be between 1 and 20.");

        RuleFor(x => x.Urgency)
            .NotEmpty()
            .Must(u => Enum.TryParse<RequestUrgency>(u, ignoreCase: true, out _))
            .WithMessage("Urgency must be 'Routine', 'Urgent', or 'Critical'.");

        RuleFor(x => x.NeededBy)
            .Must(d => d != default)
            .WithMessage("NeededBy must be a valid future timestamp.");
    }
}
