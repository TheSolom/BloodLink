namespace BloodLink.Inventory.Contracts;

/// <summary>
/// Integration event published when a blood unit is received into inventory stock.
/// Consumers must treat an unknown Status string as "not a candidate".
/// </summary>
public record UnitReceivedIntegrationEvent(
    Guid UnitId,
    string BloodType,
    string Component,
    DateTimeOffset CollectedAt,
    DateTimeOffset ExpiresAt,
    Guid BloodBankId,
    int Version
);
