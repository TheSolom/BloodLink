using BloodLink.Inventory.Api.Infrastructure.Filters;

namespace BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;

public static class ReserveUnitsEndpoint
{
    public static IEndpointRouteBuilder MapReserveUnits(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                "/api/v1/stock/reserve",
                async (
                    ReserveUnitsRequest request,
                    ReserveUnitsHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    return await handler.HandleAsync(request, cancellationToken);
                }
            )
            .AddEndpointFilter<ValidationFilter<ReserveUnitsRequest>>()
            .WithName("ReserveUnits")
            .WithSummary("Reserves blood units all-or-nothing with optimistic concurrency")
            .Produces<ReserveUnitsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
