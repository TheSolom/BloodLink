namespace BloodLink.Inventory.Api.Features.Stock.GetStockSnapshot;

public record StockSnapshotUnitDto(
    Guid UnitId,
    Guid BloodBankId,
    double Latitude,
    double Longitude,
    string BloodType,
    string Component,
    DateTimeOffset ExpiresAt,
    string Status,
    int Version
);
