namespace BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;

public record ReleaseUnitsResponse(Guid RequestId, IReadOnlyList<Guid> ReleasedUnitIds);
