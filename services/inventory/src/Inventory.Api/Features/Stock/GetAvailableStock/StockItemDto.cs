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
