namespace BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;

public record ReserveUnitsRequest(Guid RequestId, IReadOnlyList<Guid> UnitIds);
