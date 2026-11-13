using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.ValueObjects;

public sealed record GeoLocation
{
    public double Latitude { get; }
    public double Longitude { get; }

    private GeoLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static GeoLocation Create(double latitude, double longitude)
    {
        const double MinLatitude = -90.0;
        const double MaxLatitude = 90.0;
        const double MinLongitude = -180.0;
        const double MaxLongitude = 180.0;

        if (latitude is < MinLatitude or > MaxLatitude)
        {
            throw new DomainException(
                $"Latitude {latitude} is outside valid range [{MinLatitude}, {MaxLatitude}].",
                "geo_location.out_of_range"
            );
        }

        if (longitude is < MinLongitude or > MaxLongitude)
        {
            throw new DomainException(
                $"Longitude {longitude} is outside valid range [{MinLongitude}, {MaxLongitude}].",
                "geo_location.out_of_range"
            );
        }

        return new GeoLocation(latitude, longitude);
    }
}
