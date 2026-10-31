using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.Model.BloodUnits.Events;

public record UnitExpiredDomainEvent(
    Guid UnitId
) : DomainEvent;
