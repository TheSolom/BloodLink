namespace BloodLink.Inventory.Contracts;

public record ReleaseUnitsCommand(Guid RequestId, IReadOnlyList<Guid> UnitIds);
