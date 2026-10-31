using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.Model.BloodUnits.Events;

public record UnitDeliveredDomainEvent(
    Guid UnitId,
    Guid RequestId
) : DomainEvent;
