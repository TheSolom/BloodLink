using BloodLink.Inventory.Domain.ValueObjects;
using FluentValidation;

namespace BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;

public class ReceiveDonationValidator : AbstractValidator<ReceiveDonationRequest>
{
    public ReceiveDonationValidator()
    {
        RuleFor(x => x.BloodBankId)
            .NotEmpty().WithMessage("Blood bank ID is required.");

        RuleFor(x => x.DonationNumber)
            .NotEmpty().WithMessage("Donation number is required.")
            .Must(val => DonationNumber.TryParse(val, out _))
            .WithMessage("Donation number must be 3-32 alphanumeric characters with optional hyphens.");

        RuleFor(x => x.BloodType)
            .NotEmpty().WithMessage("Blood type is required.")
            .Must(code => BloodType.TryParse(code, out _))
            .WithMessage("Blood type must be a valid ABO/Rh code (e.g. 'A+', 'O-').");

        RuleFor(x => x.Component)
            .NotEmpty().WithMessage("Blood component is required.")
            .Must(comp => BloodComponentExtensions.TryParse(comp, out _))
            .WithMessage("Blood component must be either 'RedCells' or 'Platelets'.");
    }
}
