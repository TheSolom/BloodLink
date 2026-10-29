using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class ValueObjectTests
{
    private sealed class GeoLocation : ValueObject
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public GeoLocation(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Latitude;
            yield return Longitude;
        }
    }

    private sealed class DifferentValueObject : ValueObject
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public DifferentValueObject(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Latitude;
            yield return Longitude;
        }
    }

    [Fact]
    public void Equals_WhenSameValues_ReturnsTrue()
    {
        var loc1 = new GeoLocation(40.7128, -74.0060);
        var loc2 = new GeoLocation(40.7128, -74.0060);

        loc1.Equals(loc2).ShouldBeTrue();
        (loc1 == loc2).ShouldBeTrue();
        (loc1 != loc2).ShouldBeFalse();
        loc1.GetHashCode().ShouldBe(loc2.GetHashCode());
    }

    [Fact]
    public void Equals_WhenDifferentValues_ReturnsFalse()
    {
        var loc1 = new GeoLocation(40.7128, -74.0060);
        var loc2 = new GeoLocation(34.0522, -118.2437);

        loc1.Equals(loc2).ShouldBeFalse();
        (loc1 == loc2).ShouldBeFalse();
        (loc1 != loc2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentTypesWithSameValues_ReturnsFalse()
    {
        var loc1 = new GeoLocation(40.7128, -74.0060);
        var loc2 = new DifferentValueObject(40.7128, -74.0060);

        loc1.Equals(loc2).ShouldBeFalse();
        (loc1 == loc2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenComparingNull_ReturnsFalse()
    {
        var loc = new GeoLocation(40.7128, -74.0060);

        loc.Equals(null).ShouldBeFalse();
        (loc == null).ShouldBeFalse();
    }
}
