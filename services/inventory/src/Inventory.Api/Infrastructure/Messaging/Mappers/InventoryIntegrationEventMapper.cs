using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodUnits.Events;
using Riok.Mapperly.Abstractions;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging.Mappers;

/// <summary>
/// Source-generated Mapperly mapper for translating internal domain events into outward integration event contracts.
/// Per ADR-0015, Mapperly is used exclusively for outbound mappings (aggregate/domain event -> DTO/contract).
/// </summary>
[Mapper]
public static partial class InventoryIntegrationEventMapper
{
    [MapProperty(
        nameof(UnitReceivedDomainEvent.BloodType),
        nameof(UnitReceivedIntegrationEvent.BloodType),
        Use = nameof(MapBloodTypeToString)
    )]
    [MapProperty(
        nameof(UnitReceivedDomainEvent.Component),
        nameof(UnitReceivedIntegrationEvent.Component),
        Use = nameof(MapComponentToString)
    )]
    public static partial UnitReceivedIntegrationEvent ToIntegrationEvent(
        UnitReceivedDomainEvent domainEvent
    );

    public static UnitExpiredIntegrationEvent ToUnitExpiredEvent(UnitExpiredDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return new UnitExpiredIntegrationEvent(domainEvent.UnitId, domainEvent.OccurredOn);
    }

    public static UnitsReservedIntegrationEvent ToUnitsReservedEvent(
        Guid requestId,
        IReadOnlyList<Guid> unitIds
    )
    {
        ArgumentNullException.ThrowIfNull(unitIds);
        return new UnitsReservedIntegrationEvent(requestId, unitIds);
    }

    public static UnitsReleasedIntegrationEvent ToUnitsReleasedEvent(
        Guid requestId,
        IReadOnlyList<Guid> unitIds
    )
    {
        ArgumentNullException.ThrowIfNull(unitIds);
        return new UnitsReleasedIntegrationEvent(requestId, unitIds);
    }

    public static UnitStatusChangedIntegrationEvent ToUnitStatusChangedEvent(
        Guid unitId,
        string status,
        int version
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        return new UnitStatusChangedIntegrationEvent(unitId, status, version);
    }

    private static string MapBloodTypeToString(Domain.ValueObjects.BloodType bt) => bt.ToString();

    private static string MapComponentToString(Domain.ValueObjects.BloodComponent c) =>
        c.ToString();
}
