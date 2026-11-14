using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;

namespace BloodLink.Requests.Api.Infrastructure.Clients;

public class StockSnapshotBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<StockSnapshotBootstrapService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting stock snapshot bootstrap from Inventory...");

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IStockSnapshotClient client =
                scope.ServiceProvider.GetRequiredService<IStockSnapshotClient>();
            IStockReadModelService readModel =
                scope.ServiceProvider.GetRequiredService<IStockReadModelService>();

            Guid? cursor = null;
            int totalUnits = 0;

            do
            {
                GetStockSnapshotResponse? page = await client.GetSnapshotPageAsync(
                    cursor,
                    500,
                    stoppingToken
                );
                if (page == null)
                {
                    logger.LogWarning(
                        "Stock snapshot bootstrap received null response from Inventory service."
                    );
                    break;
                }

                foreach (StockSnapshotUnitDto unit in page.Items)
                {
                    await readModel.MergeSnapshotItemAsync(unit, stoppingToken);
                    totalUnits++;
                }

                cursor = page.NextCursor;
            } while (cursor.HasValue && !stoppingToken.IsCancellationRequested);

            logger.LogInformation(
                "Stock snapshot bootstrap completed. Synced {Count} units into local read model.",
                totalUnits
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Stock snapshot bootstrap encountered an error. Live events will continue syncing."
            );
        }
    }
}
