using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.Model.BloodUnits.Events;

public record UnitDispatchedDomainEvent(
    Guid UnitId,
    Guid RequestId
) : DomainEvent;
