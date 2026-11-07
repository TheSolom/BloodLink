using BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;
using FluentValidation.Results;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests.Validators;

public class ReserveUnitsValidatorTests
{
    private readonly ReserveUnitsValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_RequestIdIsEmpty()
    {
        var request = new ReserveUnitsRequest(Guid.Empty, [Guid.NewGuid()]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(request.RequestId));
    }

    [Fact]
    public void Should_HaveError_When_UnitIdsIsEmpty()
    {
        var request = new ReserveUnitsRequest(Guid.NewGuid(), []);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(request.UnitIds));
    }

    [Fact]
    public void Should_HaveError_When_UnitIdsExceeds20()
    {
        var unitIds = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList();
        var request = new ReserveUnitsRequest(Guid.NewGuid(), unitIds);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(request.UnitIds));
    }

    [Fact]
    public void Should_HaveError_When_UnitIdsContainsDuplicates()
    {
        var duplicateId = Guid.NewGuid();
        var request = new ReserveUnitsRequest(Guid.NewGuid(), [duplicateId, duplicateId]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(request.UnitIds));
    }

    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        var request = new ReserveUnitsRequest(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
