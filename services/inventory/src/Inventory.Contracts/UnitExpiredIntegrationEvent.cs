namespace BloodLink.Inventory.Contracts;

public record UnitExpiredIntegrationEvent(Guid UnitId, DateTimeOffset ExpiredAt);
