namespace BloodLink.Requests.Api.Infrastructure.Clients;

public class StockSnapshotClient(HttpClient httpClient, ILogger<StockSnapshotClient> logger)
    : IStockSnapshotClient
{
    public async Task<GetStockSnapshotResponse?> GetSnapshotPageAsync(
        Guid? cursor = null,
        int pageSize = 500,
        CancellationToken cancellationToken = default
    )
    {
        string url = cursor.HasValue
            ? $"/internal/stock-snapshot?pageSize={pageSize}&cursor={cursor.Value}"
            : $"/internal/stock-snapshot?pageSize={pageSize}";

        try
        {
            return await httpClient.GetFromJsonAsync<GetStockSnapshotResponse>(
                url,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch stock snapshot from Inventory at {Url}", url);
            return null;
        }
    }
}
