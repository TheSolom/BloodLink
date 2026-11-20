namespace BloodLink.Inventory.Api.Features.Stock.GetStockSnapshot;

public record GetStockSnapshotResponse(
    IReadOnlyList<StockSnapshotUnitDto> Items,
    Guid? NextCursor,
    bool HasMore
);
