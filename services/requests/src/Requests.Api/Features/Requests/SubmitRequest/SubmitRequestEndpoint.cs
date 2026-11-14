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
                    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                    SubmitRequestHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    if (string.IsNullOrWhiteSpace(idempotencyKey))
                    {
                        return Results.Problem(
                            statusCode: StatusCodes.Status400BadRequest,
                            title: "Missing Idempotency-Key",
                            detail: "The 'Idempotency-Key' header is required for request submission.",
                            extensions: new Dictionary<string, object?>
                            {
                                ["errorCode"] = "request.idempotency_key_required",
                            }
                        );
                    }

                    SubmitRequestResponse response = await handler.HandleAsync(
                        command,
                        idempotencyKey,
                        cancellationToken
                    );

                    return Results.Ok(response);
                }
            )
            .AddEndpointFilter<ValidationFilter<SubmitRequestCommand>>()
            .WithName("SubmitRequest")
            .WithSummary("Submits a new blood request with idempotency protection")
            .Produces<SubmitRequestResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
