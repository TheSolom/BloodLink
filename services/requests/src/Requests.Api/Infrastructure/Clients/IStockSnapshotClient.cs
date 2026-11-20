namespace BloodLink.Requests.Api.Infrastructure.Clients;

public interface IStockSnapshotClient
{
    Task<GetStockSnapshotResponse?> GetSnapshotPageAsync(
        Guid? cursor = null,
        int pageSize = 500,
        CancellationToken cancellationToken = default
    );
}
