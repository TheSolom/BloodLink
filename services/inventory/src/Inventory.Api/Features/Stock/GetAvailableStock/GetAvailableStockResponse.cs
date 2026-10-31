namespace BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;

public record StockItemDto(
    Guid Id,
    string DonationNumber,
    string BloodType,
    string Component,
    DateTimeOffset CollectedAt,
    DateTimeOffset ExpiresAt,
    string Status,
    Guid BloodBankId
);

public record GetAvailableStockResponse(
    IReadOnlyList<StockItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);
