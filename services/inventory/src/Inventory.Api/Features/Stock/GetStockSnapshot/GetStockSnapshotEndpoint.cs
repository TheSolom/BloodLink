namespace BloodLink.Inventory.Api.Features.Stock.GetStockSnapshot;

public static class GetStockSnapshotEndpoint
{
    // Internal route: must never be mapped or exposed by the gateway.
    // Gateway routing rules in Phase 8 will enforce that /internal/* routes are cluster-private.
    public static IEndpointRouteBuilder MapGetStockSnapshot(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/internal/stock-snapshot",
                async (
                    [AsParameters] GetStockSnapshotQuery query,
                    GetStockSnapshotHandler handler,
                    CancellationToken cancellationToken
                ) =>
                {
                    GetStockSnapshotResponse response = await handler.HandleAsync(
                        query,
                        cancellationToken
                    );
                    return Results.Ok(response);
                }
            )
            .WithName("GetStockSnapshot")
            .WithSummary(
                "Internal endpoint: returns non-terminal stock snapshot for Requests read-model bootstrap"
            )
            .Produces<GetStockSnapshotResponse>(StatusCodes.Status200OK);

        return endpoints;
    }
}
