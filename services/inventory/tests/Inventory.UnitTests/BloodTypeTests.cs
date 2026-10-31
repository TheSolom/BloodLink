using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests;

public class BloodTypeTests
{
    [Theory]
    [InlineData("A+", AboType.A, RhFactor.Positive)]
    [InlineData("A-", AboType.A, RhFactor.Negative)]
    [InlineData("B+", AboType.B, RhFactor.Positive)]
    [InlineData("B-", AboType.B, RhFactor.Negative)]
    [InlineData("AB+", AboType.AB, RhFactor.Positive)]
    [InlineData("AB-", AboType.AB, RhFactor.Negative)]
    [InlineData("O+", AboType.O, RhFactor.Positive)]
    [InlineData("O-", AboType.O, RhFactor.Negative)]
    public void TryParse_ValidCode_ReturnsTrueAndCorrectBloodType(string input, AboType expectedAbo, RhFactor expectedRh)
    {
        bool success = BloodType.TryParse(input, out BloodType? bloodType);

        success.ShouldBeTrue();
        bloodType.ShouldNotBeNull();
        bloodType.Abo.ShouldBe(expectedAbo);
        bloodType.Rh.ShouldBe(expectedRh);
        bloodType.ToString().ShouldBe(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("C+")]
    [InlineData("A")]
    [InlineData("O*")]
    [InlineData("AB")]
    [InlineData("INVALID")]
    public void TryParse_InvalidCode_ReturnsFalse(string? input)
    {
        bool success = BloodType.TryParse(input, out BloodType? bloodType);

        success.ShouldBeFalse();
        bloodType.ShouldBeNull();
    }

    [Fact]
    public void Parse_ValidCode_ReturnsInstance()
    {
        var bloodType = BloodType.Parse("O-");

        bloodType.Abo.ShouldBe(AboType.O);
        bloodType.Rh.ShouldBe(RhFactor.Negative);
        bloodType.ToString().ShouldBe("O-");
    }

    [Fact]
    public void Parse_InvalidCode_ThrowsDomainException()
    {
        var ex = Should.Throw<DomainException>(() => BloodType.Parse("XYZ"));
        ex.Code.ShouldBe("blood_type.invalid");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var bt1 = BloodType.Parse("A+");
        var bt2 = BloodType.Parse("A+");
        var bt3 = BloodType.Parse("A-");

        bt1.ShouldBe(bt2);
        (bt1 == bt2).ShouldBeTrue();
        (bt1 == bt3).ShouldBeFalse();
        bt1.GetHashCode().ShouldBe(bt2.GetHashCode());
    }
}
