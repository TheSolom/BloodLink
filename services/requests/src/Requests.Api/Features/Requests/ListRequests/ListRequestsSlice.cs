using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Requests.Api.Features.Requests.ListRequests;

public record ListRequestsQuery(int Page = 1, int PageSize = 20);

public record RequestSummaryDto(
    Guid Id,
    Guid HospitalId,
    string BloodType,
    string Component,
    int UnitsNeeded,
    string Urgency,
    DateTimeOffset NeededBy,
    string Status,
    string IdempotencyKey,
    int AssignedUnitsCount
);

public record ListRequestsResponse(
    IReadOnlyList<RequestSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public class ListRequestsHandler(RequestsDbContext dbContext, IHospitalContext hospitalContext)
{
    public async Task<ListRequestsResponse> HandleAsync(
        ListRequestsQuery query,
        CancellationToken cancellationToken
    )
    {
        Guid hospitalId = hospitalContext.GetHospitalId();
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<BloodRequest> baseQuery = dbContext
            .BloodRequests.AsNoTracking()
            .Where(r => r.HospitalId == hospitalId);

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        List<BloodRequest> requests = await baseQuery
            .OrderByDescending(r => r.NeededBy)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        List<RequestSummaryDto> items = requests
            .Select(r => new RequestSummaryDto(
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

        return new ListRequestsResponse(items, page, pageSize, totalCount);
    }
}

public static class ListRequestsEndpoint
{
    public static IEndpointRouteBuilder MapListRequests(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/v1/requests",
                async (
                    [AsParameters] ListRequestsQuery query,
                    ListRequestsHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    ListRequestsResponse response = await handler.HandleAsync(
                        query,
                        cancellationToken
                    );
                    return Results.Ok(response);
                }
            )
            .WithName("ListRequests")
            .WithSummary("Lists blood requests for the authenticated hospital")
            .Produces<ListRequestsResponse>(StatusCodes.Status200OK);

        return endpoints;
    }
}
