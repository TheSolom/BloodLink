using BloodLink.Requests.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Primitives;

namespace BloodLink.Requests.Api.Infrastructure.Auth;

/// <summary>
/// Development stub for obtaining the calling hospital's identity.
/// In Phase 8, this will be replaced with real JWT claims extraction.
/// For now, reads the 'X-Hospital-Id' header, or defaults to seeded St. Thomas' Hospital.
/// </summary>
public class DevHospitalContext(IHttpContextAccessor httpContextAccessor) : IHospitalContext
{
    public const string HospitalIdHeaderName = "X-Hospital-Id";

    public Guid GetHospitalId()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (
            httpContext != null
            && httpContext.Request.Headers.TryGetValue(
                HospitalIdHeaderName,
                out StringValues values
            )
            && Guid.TryParse(values.ToString(), out Guid parsedId)
        )
        {
            return parsedId;
        }

        // Default to seeded St. Thomas' Hospital in development
        return SeededHospitals.StThomasId;
    }
}
