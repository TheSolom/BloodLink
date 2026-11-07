namespace BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;

public record ReserveUnitsResponse(Guid RequestId, IReadOnlyList<Guid> ReservedUnitIds);
