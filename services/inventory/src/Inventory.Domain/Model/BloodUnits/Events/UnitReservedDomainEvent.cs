using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.Model.BloodUnits.Events;

public record UnitReservedDomainEvent(
    Guid UnitId,
    Guid RequestId
) : DomainEvent;
