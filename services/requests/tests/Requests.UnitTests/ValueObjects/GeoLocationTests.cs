using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.UnitTests.ValueObjects;

public class GeoLocationTests
{
    [Theory]
    [InlineData(51.5074, -0.1278)]
    [InlineData(0.0, 0.0)]
    [InlineData(-90.0, -180.0)]
    [InlineData(90.0, 180.0)]
    public void Constructor_ValidCoordinates_CreatesInstance(double lat, double lon)
    {
        var location = new GeoLocation(lat, lon);
        location.Latitude.ShouldBe(lat);
        location.Longitude.ShouldBe(lon);
    }

    [Theory]
    [InlineData(double.NaN, 0.0)]
    [InlineData(0.0, double.NaN)]
    [InlineData(double.NaN, double.NaN)]
    [InlineData(90.1, 0.0)]
    [InlineData(-90.1, 0.0)]
    [InlineData(0.0, 180.1)]
    [InlineData(0.0, -180.1)]
    public void Constructor_InvalidOrNaNCoordinates_ThrowsDomainException(double lat, double lon)
    {
        Should.Throw<DomainException>(() => new GeoLocation(lat, lon));
    }
}
