using BloodLink.BuildingBlocks.Web;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;

public static class GetAvailableStockEndpoint
{
    public static IEndpointRouteBuilder MapGetAvailableStock(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/v1/stock",
                async Task<Ok<GetAvailableStockResponse>> (
                    [AsParameters] GetAvailableStockQuery query,
                    GetAvailableStockHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    GetAvailableStockResponse response = await handler.HandleAsync(
                        query,
                        cancellationToken
                    );
                    return TypedResults.Ok(response);
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
