using BloodLink.BuildingBlocks.Domain;
using BloodLink.BuildingBlocks.Web;
using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Requests.Api.Features.Requests.ListRequests;

public class ListRequestsHandler(RequestsDbContext dbContext, IHospitalContext hospitalContext)
{
    public async Task<ListRequestsResponse> HandleAsync(
        ListRequestsQuery query,
        CancellationToken cancellationToken
    )
    {
        Guid hospitalId = hospitalContext.GetHospitalId();

        IQueryable<BloodRequest> baseQuery = dbContext
            .BloodRequests.AsNoTracking()
            .Where(r => r.HospitalId == hospitalId);

        PagedResult<BloodRequest> paged = await baseQuery
            .OrderByDescending(r => r.NeededBy)
            .ThenBy(r => r.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        var items = paged
            .Items.Select(r => new RequestSummaryDto(
                r.Id,
                r.HospitalId,
                r.BloodType.ToString(),
                r.Component.ToString(),
                r.UnitsNeeded,
                r.Urgency.ToString(),
                r.NeededBy,
                r.Status.ToString(),
                r.IdempotencyKey,
                r.AssignedUnitIds.Count
            ))
            .ToList();

        return new ListRequestsResponse(items, paged.Page, paged.PageSize, paged.TotalCount);
    }
}
