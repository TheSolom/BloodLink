namespace BloodLink.Requests.Api.Infrastructure.Auth;

/// <summary>
/// Obtains the calling hospital's identity.
/// </summary>
public interface IHospitalContext
{
    Guid GetHospitalId();
}
