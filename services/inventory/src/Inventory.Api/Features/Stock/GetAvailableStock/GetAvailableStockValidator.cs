using BloodLink.Inventory.Domain.ValueObjects;
using FluentValidation;

namespace BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;

public class GetAvailableStockValidator : AbstractValidator<GetAvailableStockQuery>
{
    public GetAvailableStockValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        When(x => !string.IsNullOrWhiteSpace(x.BloodType), () =>
        {
            RuleFor(x => x.BloodType)
                .Must(code => BloodType.TryParse(code, out _))
                .WithMessage("Blood type filter must be a valid ABO/Rh code (e.g. 'A+', 'O-').");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Component), () =>
        {
            RuleFor(x => x.Component)
                .Must(comp => BloodComponentExtensions.TryParse(comp, out _))
                .WithMessage("Component filter must be either 'RedCells' or 'Platelets'.");
        });
    }
}
