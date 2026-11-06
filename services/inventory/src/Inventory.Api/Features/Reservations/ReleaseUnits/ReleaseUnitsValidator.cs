using FluentValidation;

namespace BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;

public class ReleaseUnitsValidator : AbstractValidator<ReleaseUnitsRequest>
{
    public ReleaseUnitsValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");

        RuleFor(x => x.UnitIds).NotEmpty().WithMessage("At least one UnitId is required.");

        RuleFor(x => x.UnitIds)
            .Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
            .WithMessage("UnitIds must not contain duplicate identifiers.");
    }
}
