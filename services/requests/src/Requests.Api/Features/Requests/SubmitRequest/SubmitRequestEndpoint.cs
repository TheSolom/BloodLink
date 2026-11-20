using BloodLink.BuildingBlocks.Domain;
using BloodLink.BuildingBlocks.Web;
using Microsoft.AspNetCore.Mvc;

namespace BloodLink.Requests.Api.Features.Requests.SubmitRequest;

public static class SubmitRequestEndpoint
{
    public static IEndpointRouteBuilder MapSubmitRequest(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                "/api/v1/requests",
                async (
                    [FromBody] SubmitRequestCommand command,
                    [FromHeader(Name = IdempotencyKey.HeaderName)] string? idempotencyKey,
                    SubmitRequestHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    SubmitRequestResponse response = await handler.HandleAsync(
                        command,
                        idempotencyKey!,
                        cancellationToken
                    );

                    return Results.Ok(response);
                }
            )
            .RequireIdempotencyKey()
            .AddEndpointFilter<ValidationFilter<SubmitRequestCommand>>()
            .WithName("SubmitRequest")
            .WithSummary("Submits a new blood request with idempotency protection")
            .Produces<SubmitRequestResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
