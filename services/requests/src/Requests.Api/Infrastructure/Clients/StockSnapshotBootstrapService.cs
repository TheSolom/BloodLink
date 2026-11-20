using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;

namespace BloodLink.Requests.Api.Infrastructure.Clients;

public class StockSnapshotBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<StockSnapshotBootstrapService> logger
) : BackgroundService
{
    private const int DefaultBatchPageSize = 500;
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting stock snapshot bootstrap from Inventory...");

        for (
            int attempt = 1;
            attempt <= MaxRetryAttempts && !stoppingToken.IsCancellationRequested;
            attempt++
        )
        {
            if (await TrySyncAttemptAsync(attempt, stoppingToken))
            {
                return;
            }

            if (await TryWaitBeforeRetryAsync(stoppingToken))
            {
                break;
            }
        }

        logger.LogWarning(
            "Stock snapshot bootstrap could not complete all pages. Live events will continue syncing."
        );
    }

    private async Task<bool> TrySyncAttemptAsync(int attempt, CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IStockSnapshotClient client =
                scope.ServiceProvider.GetRequiredService<IStockSnapshotClient>();
            IStockReadModelService readModel =
                scope.ServiceProvider.GetRequiredService<IStockReadModelService>();

            int? syncedCount = await FetchAndMergeAllPagesAsync(
                client,
                readModel,
                attempt,
                stoppingToken
            );

            if (syncedCount.HasValue)
            {
                logger.LogInformation(
                    "Stock snapshot bootstrap completed. Synced {Count} units into local read model.",
                    syncedCount.Value
                );
                return true;
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Stock snapshot bootstrap attempt {Attempt} encountered an error. Retrying in {Delay}s...",
                attempt,
                RetryDelay.TotalSeconds
            );
        }

        return false;
    }

    private async Task<int?> FetchAndMergeAllPagesAsync(
        IStockSnapshotClient client,
        IStockReadModelService readModel,
        int attempt,
        CancellationToken stoppingToken
    )
    {
        Guid? cursor = null;
        int totalUnits = 0;

        do
        {
            GetStockSnapshotResponse? page = await client.GetSnapshotPageAsync(
                cursor,
                DefaultBatchPageSize,
                stoppingToken
            );

            if (page == null)
            {
                logger.LogWarning(
                    "Stock snapshot bootstrap attempt {Attempt} received null response from Inventory service.",
                    attempt
                );
                return null;
            }

            await MergePageItemsAsync(readModel, page.Items, stoppingToken);
            totalUnits += page.Items.Count;
            cursor = page.NextCursor;
        } while (cursor.HasValue && !stoppingToken.IsCancellationRequested);

        return totalUnits;
    }

    private static async Task MergePageItemsAsync(
        IStockReadModelService readModel,
        IEnumerable<StockSnapshotUnitDto> items,
        CancellationToken stoppingToken
    )
    {
        foreach (StockSnapshotUnitDto unit in items)
        {
            await readModel.MergeSnapshotItemAsync(unit, stoppingToken);
        }
    }

    private static async Task<bool> TryWaitBeforeRetryAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(RetryDelay, stoppingToken);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }
}
