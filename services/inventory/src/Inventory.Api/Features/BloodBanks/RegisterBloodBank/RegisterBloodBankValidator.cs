using FluentValidation;

namespace BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;

public class RegisterBloodBankValidator : AbstractValidator<RegisterBloodBankRequest>
{
    public RegisterBloodBankValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0)
            .WithMessage("Latitude must be between -90 and 90 degrees.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0)
            .WithMessage("Longitude must be between -180 and 180 degrees.");
    }
}
