using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests;

public class GeoLocationTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(40.7128, -74.0060)]
    public void Create_ValidCoordinates_Succeeds(double lat, double lon)
    {
        var location = GeoLocation.Create(lat, lon);

        location.Latitude.ShouldBe(lat);
        location.Longitude.ShouldBe(lon);
    }

    [Theory]
    [InlineData(-90.001, 0)]
    [InlineData(90.001, 0)]
    [InlineData(0, -180.001)]
    [InlineData(0, 180.001)]
    public void Create_OutOfRangeCoordinates_ThrowsDomainException(double lat, double lon)
    {
        var ex = Should.Throw<DomainException>(() => GeoLocation.Create(lat, lon));
        ex.Code.ShouldBe("geo_location.out_of_range");
    }

    [Fact]
    public void Equality_SameCoordinates_AreEqual()
    {
        var loc1 = GeoLocation.Create(51.5074, -0.1278);
        var loc2 = GeoLocation.Create(51.5074, -0.1278);
        var loc3 = GeoLocation.Create(51.5075, -0.1278);

        loc1.ShouldBe(loc2);
        (loc1 == loc2).ShouldBeTrue();
        (loc1 == loc3).ShouldBeFalse();
        loc1.GetHashCode().ShouldBe(loc2.GetHashCode());
    }
}
