using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Api.Infrastructure.Messaging.Mappers;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.Model.BloodUnits.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging;

/// <summary>
/// EF Core SaveChangesInterceptor that captures domain events raised by aggregates
/// and dispatches corresponding integration events into the MassTransit Transactional Outbox
/// before SaveChanges commits, guaranteeing zero dual-write anomalies per ADR-0006.
/// </summary>
public class DomainEventOutboxInterceptor : SaveChangesInterceptor
{
    private readonly Func<IEventPublisher> _eventPublisherProvider;

    public DomainEventOutboxInterceptor(IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);
        _eventPublisherProvider = () => eventPublisher;
    }

    public DomainEventOutboxInterceptor(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _eventPublisherProvider = () => serviceProvider.GetRequiredService<IEventPublisher>();
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public async Task DispatchDomainEventsAsync(
        DbContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        List<object> integrationEvents = ExtractIntegrationEvents(context);
        if (integrationEvents.Count == 0)
        {
            return;
        }

        IEventPublisher eventPublisher = _eventPublisherProvider();
        foreach (object evt in integrationEvents)
        {
            await eventPublisher.PublishAsync(evt, cancellationToken);
        }
    }

    private static List<object> ExtractIntegrationEvents(DbContext context)
    {
        var entries = context
            .ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        var integrationEvents = new List<object>();

        foreach (EntityEntry<IHasDomainEvents> entry in entries)
        {
            IReadOnlyCollection<IDomainEvent> domainEvents = entry.Entity.ClearDomainEvents();
            foreach (IDomainEvent domainEvent in domainEvents)
            {
                MapDomainEvent(domainEvent, entry.Entity, integrationEvents);
            }
        }

        return integrationEvents;
    }

    private static void MapDomainEvent(
        IDomainEvent domainEvent,
        IHasDomainEvents entity,
        List<object> integrationEvents
    )
    {
        switch (domainEvent)
        {
            case UnitReceivedDomainEvent received:
                integrationEvents.Add(InventoryIntegrationEventMapper.ToIntegrationEvent(received));
                AddStatusChangedEvent(entity, integrationEvents);
                break;

            case UnitExpiredDomainEvent expired:
                integrationEvents.Add(InventoryIntegrationEventMapper.ToUnitExpiredEvent(expired));
                AddStatusChangedEvent(entity, integrationEvents);
                break;

            case UnitReservedDomainEvent:
            case UnitReservationReleasedDomainEvent:
            case UnitDispatchedDomainEvent:
            case UnitDeliveredDomainEvent:
                AddStatusChangedEvent(entity, integrationEvents);
                break;
        }
    }

    private static void AddStatusChangedEvent(
        IHasDomainEvents entity,
        List<object> integrationEvents
    )
    {
        if (entity is BloodUnit unit)
        {
            integrationEvents.Add(
                InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(
                    unit.Id,
                    unit.Status.ToString(),
                    unit.Version
                )
            );
        }
    }
}
