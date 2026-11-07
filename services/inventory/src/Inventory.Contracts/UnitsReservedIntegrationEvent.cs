namespace BloodLink.Inventory.Contracts;

public record UnitsReservedIntegrationEvent(Guid RequestId, IReadOnlyList<Guid> UnitIds);
