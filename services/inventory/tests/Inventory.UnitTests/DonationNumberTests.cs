using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests;

public class DonationNumberTests
{
    [Theory]
    [InlineData("DN-12345", "DN-12345")]
    [InlineData("  dn-12345  ", "DN-12345")]
    [InlineData("abc", "ABC")]
    [InlineData("12345678901234567890123456789012", "12345678901234567890123456789012")]
    public void Create_ValidFormat_TrimsAndUppercases(string input, string expected)
    {
        var donationNumber = DonationNumber.Create(input);

        donationNumber.Value.ShouldBe(expected);
        donationNumber.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")] // Too short (< 3)
    [InlineData("123456789012345678901234567890123")] // Too long (> 32)
    [InlineData("DN 12345")] // Invalid space
    [InlineData("DN#12345")] // Invalid symbol
    [InlineData("DN_12345")] // Underscore not allowed, only hyphens
    public void Create_InvalidFormat_ThrowsDomainException(string? input)
    {
        DomainException ex = Should.Throw<DomainException>(() => DonationNumber.Create(input!));
        ex.Code.ShouldBe("donation_number.invalid");
    }

    [Fact]
    public void TryParse_Valid_ReturnsTrue()
    {
        bool success = DonationNumber.TryParse("  dn-999  ", out DonationNumber? result);

        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Value.ShouldBe("DN-999");
    }

    [Fact]
    public void TryParse_Invalid_ReturnsFalse()
    {
        bool success = DonationNumber.TryParse("invalid*num", out DonationNumber? result);

        success.ShouldBeFalse();
        result.ShouldBeNull();
    }
}
