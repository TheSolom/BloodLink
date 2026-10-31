using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.Model.BloodUnits.Events;

public record UnitReservationReleasedDomainEvent(
    Guid UnitId,
    Guid RequestId
) : DomainEvent;
