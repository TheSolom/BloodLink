using BloodLink.Requests.Domain.Hospitals;
using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Api.Infrastructure.Persistence;

public static class SeededHospitals
{
    public static readonly Guid StThomasId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid RoyalLondonId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid KingsCollegeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Hospital StThomas = new(
        StThomasId,
        "St. Thomas' Hospital",
        new GeoLocation(51.4988, -0.1186)
    );

    public static readonly Hospital RoyalLondon = new(
        RoyalLondonId,
        "Royal London Hospital",
        new GeoLocation(51.5190, -0.0595)
    );

    public static readonly Hospital KingsCollege = new(
        KingsCollegeId,
        "King's College Hospital",
        new GeoLocation(51.4682, -0.0934)
    );

    public static readonly IReadOnlyList<Hospital> All = [StThomas, RoyalLondon, KingsCollege];
}
