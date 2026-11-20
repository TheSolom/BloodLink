using BloodLink.Inventory.Contracts;
using BloodLink.Requests.Api.Infrastructure.Clients;
using BloodLink.Requests.Domain.Matching;

namespace BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;

public interface IStockReadModelService
{
    Task ApplyUnitReceivedAsync(
        UnitReceivedIntegrationEvent unitReceived,
        CancellationToken ct = default
    );
    Task ApplyUnitStatusChangedAsync(
        UnitStatusChangedIntegrationEvent unitStatusChanged,
        CancellationToken ct = default
    );
    Task MergeSnapshotItemAsync(StockSnapshotUnitDto snapshot, CancellationToken ct = default);
    Task<IReadOnlyList<CandidateUnitSnapshot>> GetAvailableCandidateUnitsAsync(
        CancellationToken ct = default
    );
}
