using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests;

public class BloodComponentTests
{
    [Fact]
    public void RedCells_Has42DaysShelfLife()
    {
        BloodComponent.RedCells.GetShelfLife().ShouldBe(TimeSpan.FromDays(42));
    }

    [Fact]
    public void Platelets_Has5DaysShelfLife()
    {
        BloodComponent.Platelets.GetShelfLife().ShouldBe(TimeSpan.FromDays(5));
    }

    [Theory]
    [InlineData("RedCells", BloodComponent.RedCells)]
    [InlineData("redcells", BloodComponent.RedCells)]
    [InlineData("Platelets", BloodComponent.Platelets)]
    [InlineData("platelets", BloodComponent.Platelets)]
    public void TryParse_ValidComponent_ReturnsTrueAndCorrectEnum(string input, BloodComponent expected)
    {
        bool success = BloodComponentExtensions.TryParse(input, out BloodComponent component);

        success.ShouldBeTrue();
        component.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Plasma")]
    [InlineData("WholeBlood")]
    public void TryParse_InvalidComponent_ReturnsFalse(string? input)
    {
        bool success = BloodComponentExtensions.TryParse(input, out _);

        success.ShouldBeFalse();
    }
}
