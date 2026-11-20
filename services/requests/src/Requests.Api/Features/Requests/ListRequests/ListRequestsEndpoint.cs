using Microsoft.AspNetCore.Http.HttpResults;

namespace BloodLink.Requests.Api.Features.Requests.ListRequests;

public static class ListRequestsEndpoint
{
    public static IEndpointRouteBuilder MapListRequests(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/v1/requests",
                async Task<Ok<ListRequestsResponse>> (
                    [AsParameters] ListRequestsQuery query,
                    ListRequestsHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    ListRequestsResponse response = await handler.HandleAsync(
                        query,
                        cancellationToken
                    );
                    return TypedResults.Ok(response);
                }
            )
            .WithName("ListRequests")
            .WithSummary("Lists blood requests for the authenticated hospital")
            .Produces<ListRequestsResponse>(StatusCodes.Status200OK);

        return endpoints;
    }
}
