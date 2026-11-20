using BloodLink.Requests.Api.Infrastructure.Auth;

namespace BloodLink.Requests.UnitTests.Slices;

public sealed class TestHospitalContext(Guid hospitalId) : IHospitalContext
{
    public Guid GetHospitalId() => hospitalId;
}
