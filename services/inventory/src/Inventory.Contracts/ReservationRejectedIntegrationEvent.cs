namespace BloodLink.Inventory.Contracts;

public record ReservationRejectedIntegrationEvent(
    Guid RequestId,
    IReadOnlyList<Guid> UnavailableUnitIds,
    string Reason
);
