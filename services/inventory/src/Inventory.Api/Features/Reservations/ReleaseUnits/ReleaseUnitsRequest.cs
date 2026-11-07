namespace BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;

public record ReleaseUnitsRequest(Guid RequestId, IReadOnlyList<Guid> UnitIds);
