using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BloodLink.Requests.Api.Features.Requests.GetRequestStatus;

public static class GetRequestStatusEndpoint
{
    public static IEndpointRouteBuilder MapGetRequestStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/v1/requests/{id:guid}",
                async (
                    [FromRoute] Guid id,
                    GetRequestStatusHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    RequestStatusResponse? response = await handler.HandleAsync(
                        id,
                        cancellationToken
                    );
                    return response is not null ? Results.Ok(response) : Results.NotFound();
                }
            )
            .WithName("GetRequestStatus")
            .WithSummary("Retrieves status and details of a blood request")
            .Produces<RequestStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
