using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Requests.Api.Features.Requests.CancelRequest;

public record CancelRequestCommand(string Reason);

public class CancelRequestHandler(RequestsDbContext dbContext, IHospitalContext hospitalContext)
{
    public async Task HandleAsync(
        Guid requestId,
        CancelRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        Guid hospitalId = hospitalContext.GetHospitalId();

        BloodRequest? request = await dbContext.BloodRequests.FirstOrDefaultAsync(
            r => r.Id == requestId,
            cancellationToken
        );

        if (request == null)
        {
            throw new DomainException(
                $"Blood request '{requestId}' was not found.",
                "request.not_found"
            );
        }

        if (request.HospitalId != hospitalId)
        {
            throw new DomainException(
                "You do not have permission to cancel this request.",
                "request.forbidden"
            );
        }

        request.Cancel(command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
