namespace BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;

public record GetAvailableStockQuery(
    string? BloodType = null,
    string? Component = null,
    Guid? BankId = null,
    DateTimeOffset? ExpiringBefore = null,
    int Page = 1,
    int PageSize = 20
);
