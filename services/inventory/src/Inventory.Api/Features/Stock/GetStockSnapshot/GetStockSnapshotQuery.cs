namespace BloodLink.Inventory.Api.Features.Stock.GetStockSnapshot;

public record GetStockSnapshotQuery(Guid? Cursor = null, int PageSize = 100);
