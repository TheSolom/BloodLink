namespace BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;

public record GetAvailableStockResponse(
    IReadOnlyList<StockItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);
