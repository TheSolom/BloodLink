using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class ValueObjectTests
{
    private sealed record GeoLocation(double Latitude, double Longitude) : ValueObject;

    private sealed record DifferentValueObject(double Latitude, double Longitude) : ValueObject;

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
        (null == loc).ShouldBeFalse();
    }

    [Fact]
    public void WithExpression_SupportsNonDestructiveMutation()
    {
        var original = new GeoLocation(40.7128, -74.0060);
        var modified = original with { Latitude = 41.0 };

        modified.Latitude.ShouldBe(41.0);
        modified.Longitude.ShouldBe(-74.0060);
        original.Latitude.ShouldBe(40.7128); // original unchanged
        (original == modified).ShouldBeFalse();
    }
}
