using BloodLink.Inventory.Api.Infrastructure.Filters;

namespace BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;

public static class ReleaseUnitsEndpoint
{
    public static IEndpointRouteBuilder MapReleaseUnits(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                "/api/v1/stock/release",
                async (
                    ReleaseUnitsRequest request,
                    ReleaseUnitsHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    return await handler.HandleAsync(request, cancellationToken);
                }
            )
            .AddEndpointFilter<ValidationFilter<ReleaseUnitsRequest>>()
            .WithName("ReleaseUnits")
            .WithSummary("Releases blood units previously reserved by a request")
            .Produces<ReleaseUnitsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
