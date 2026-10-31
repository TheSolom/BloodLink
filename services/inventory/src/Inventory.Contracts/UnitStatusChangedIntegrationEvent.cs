namespace BloodLink.Inventory.Contracts;

public record UnitStatusChangedIntegrationEvent(
    Guid UnitId,
    string Status,
    int Version
);
