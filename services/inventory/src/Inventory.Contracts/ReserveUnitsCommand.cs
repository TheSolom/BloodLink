namespace BloodLink.Inventory.Contracts;

public record ReserveUnitsCommand(Guid RequestId, IReadOnlyList<Guid> UnitIds);
