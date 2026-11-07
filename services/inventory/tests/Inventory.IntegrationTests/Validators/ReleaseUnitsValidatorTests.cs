using BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;
using FluentValidation.Results;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests.Validators;

public class ReleaseUnitsValidatorTests
{
    private readonly ReleaseUnitsValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_RequestIdIsEmpty()
    {
        var request = new ReleaseUnitsRequest(Guid.Empty, [Guid.NewGuid()]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(request.RequestId));
    }

    [Fact]
    public void Should_HaveError_When_UnitIdsIsEmpty()
    {
        var request = new ReleaseUnitsRequest(Guid.NewGuid(), []);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(request.UnitIds));
    }

    [Fact]
    public void Should_HaveError_When_UnitIdsContainsDuplicates()
    {
        var duplicateId = Guid.NewGuid();
        var request = new ReleaseUnitsRequest(Guid.NewGuid(), [duplicateId, duplicateId]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(request.UnitIds));
    }

    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        var request = new ReleaseUnitsRequest(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
