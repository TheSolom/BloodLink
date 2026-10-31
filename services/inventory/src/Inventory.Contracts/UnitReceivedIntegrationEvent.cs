namespace BloodLink.Inventory.Contracts;

public record UnitReceivedIntegrationEvent(
    Guid UnitId,
    string BloodType,
    string Component,
    DateTimeOffset CollectedAt,
    DateTimeOffset ExpiresAt,
    Guid BloodBankId
);
