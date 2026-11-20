using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.ValueObjects;

public sealed record GeoLocation
{
    public double Latitude { get; }
    public double Longitude { get; }

    private const double EarthRadiusKm = 6371.0;

    public GeoLocation(double latitude, double longitude)
    {
        const double MinLatitude = -90.0;
        const double MaxLatitude = 90.0;
        const double MinLongitude = -180.0;
        const double MaxLongitude = 180.0;

        if (double.IsNaN(latitude) || latitude is < MinLatitude or > MaxLatitude)
        {
            throw new DomainException(
                $"Latitude must be between {MinLatitude} and {MaxLatitude} degrees. Received: {latitude}.",
                Errors.RequestsDomainErrors.GeoLocation.InvalidLatitude
            );
        }

        if (double.IsNaN(longitude) || longitude is < MinLongitude or > MaxLongitude)
        {
            throw new DomainException(
                $"Longitude must be between {MinLongitude} and {MaxLongitude} degrees. Received: {longitude}.",
                Errors.RequestsDomainErrors.GeoLocation.InvalidLongitude
            );
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public static GeoLocation Create(double latitude, double longitude) => new(latitude, longitude);

    public double DistanceToKm(GeoLocation other)
    {
        ArgumentNullException.ThrowIfNull(other);

        double dLat = ToRadians(other.Latitude - Latitude);
        double dLon = ToRadians(other.Longitude - Longitude);
        double lat1 = ToRadians(Latitude);
        double lat2 = ToRadians(other.Latitude);

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
