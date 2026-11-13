using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.ValueObjects;
using FluentValidation;

namespace BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;

public class RegisterBloodBankValidator : AbstractValidator<RegisterBloodBankRequest>
{
    public RegisterBloodBankValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(BloodBank.NameMaxLength)
            .WithMessage($"Name cannot exceed {BloodBank.NameMaxLength} characters.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(GeoLocation.MinLatitude, GeoLocation.MaxLatitude)
            .WithMessage(
                $"Latitude must be between {GeoLocation.MinLatitude} and {GeoLocation.MaxLatitude} degrees."
            );

        RuleFor(x => x.Longitude)
            .InclusiveBetween(GeoLocation.MinLongitude, GeoLocation.MaxLongitude)
            .WithMessage(
                $"Longitude must be between {GeoLocation.MinLongitude} and {GeoLocation.MaxLongitude} degrees."
            );
    }
}
