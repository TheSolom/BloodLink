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
        if (latitude is < -90.0 or > 90.0)
        {
            throw new DomainException($"Latitude {latitude} is outside valid range [-90, 90].", "geo_location.out_of_range");
        }

        if (longitude is < -180.0 or > 180.0)
        {
            throw new DomainException($"Longitude {longitude} is outside valid range [-180, 180].", "geo_location.out_of_range");
        }

        return new GeoLocation(latitude, longitude);
    }
}
