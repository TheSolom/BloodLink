using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.ValueObjects;

public sealed record GeoLocation
{
    public const double MinLatitude = -90.0;
    public const double MaxLatitude = 90.0;
    public const double MinLongitude = -180.0;
    public const double MaxLongitude = 180.0;

    public double Latitude { get; }
    public double Longitude { get; }

    private GeoLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static GeoLocation Create(double latitude, double longitude)
    {
        if (latitude is < MinLatitude or > MaxLatitude)
        {
            throw new DomainException(
                $"Latitude {latitude} is outside valid range [{MinLatitude}, {MaxLatitude}].",
                Errors.InventoryDomainErrors.GeoLocation.OutOfRange
            );
        }

        if (longitude is < MinLongitude or > MaxLongitude)
        {
            throw new DomainException(
                $"Longitude {longitude} is outside valid range [{MinLongitude}, {MaxLongitude}].",
                Errors.InventoryDomainErrors.GeoLocation.OutOfRange
            );
        }

        return new GeoLocation(latitude, longitude);
    }
}
