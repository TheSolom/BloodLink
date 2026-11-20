namespace BloodLink.Requests.Api.Infrastructure.Clients;

public record GetStockSnapshotResponse(
    IReadOnlyList<StockSnapshotUnitDto> Items,
    Guid? NextCursor,
    bool HasMore
);
