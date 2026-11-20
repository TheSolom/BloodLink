using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Requests.Api.Features.Requests.GetRequestStatus;

public class GetRequestStatusHandler(RequestsDbContext dbContext, IHospitalContext hospitalContext)
{
    public async Task<RequestStatusResponse?> HandleAsync(
        Guid requestId,
        CancellationToken cancellationToken
    )
    {
        Guid hospitalId = hospitalContext.GetHospitalId();

        BloodRequest? request = await dbContext
            .BloodRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
        {
            return null;
        }

        if (request.HospitalId != hospitalId)
        {
            throw new DomainException(
                "You do not have permission to view this request.",
                "request.forbidden"
            );
        }

        return GetRequestStatusMapper.ToResponse(request);
    }
}
