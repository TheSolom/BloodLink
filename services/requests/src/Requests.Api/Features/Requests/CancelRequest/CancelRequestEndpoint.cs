using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Requests.Api.Features.Requests.CancelRequest;

public static class CancelRequestEndpoint
{
    public static IEndpointRouteBuilder MapCancelRequest(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                "/api/v1/requests/{id:guid}/cancel",
                async (
                    [FromRoute] Guid id,
                    [FromBody] CancelRequestCommand command,
                    CancelRequestHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    await handler.HandleAsync(id, command, cancellationToken);
                    return Results.Ok();
                }
            )
            .WithName("CancelRequest")
            .WithSummary("Cancels a blood request before it enters in-transit status")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
