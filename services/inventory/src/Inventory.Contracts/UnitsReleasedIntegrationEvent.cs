namespace BloodLink.Inventory.Contracts;

public record UnitsReleasedIntegrationEvent(Guid RequestId, IReadOnlyList<Guid> UnitIds);
