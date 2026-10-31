using BloodLink.Inventory.Api.Infrastructure.Filters;

namespace BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;

public static class GetAvailableStockEndpoint
{
    public static IEndpointRouteBuilder MapGetAvailableStock(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/v1/stock",
                async (
                    [AsParameters] GetAvailableStockQuery query,
                    GetAvailableStockHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    GetAvailableStockResponse response = await handler.HandleAsync(
                        query,
                        cancellationToken
                    );
                    return Results.Ok(response);
                }
            )
            .AddEndpointFilter<ValidationFilter<GetAvailableStockQuery>>()
            .WithName("GetAvailableStock")
            .WithSummary("Retrieves available, non-expired blood stock")
            .Produces<GetAvailableStockResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
