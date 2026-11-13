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

        var entries = context
            .ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var integrationEvents = new List<object>();

        foreach (EntityEntry<IHasDomainEvents>? entry in entries)
        {
            IReadOnlyCollection<IDomainEvent> domainEvents = entry.Entity.ClearDomainEvents();

            foreach (IDomainEvent domainEvent in domainEvents)
            {
                switch (domainEvent)
                {
                    case UnitReceivedDomainEvent received:
                        integrationEvents.Add(
                            InventoryIntegrationEventMapper.ToIntegrationEvent(received)
                        );
                        if (entry.Entity is BloodUnit unitR)
                        {
                            integrationEvents.Add(
                                InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(
                                    unitR.Id,
                                    unitR.Status.ToString(),
                                    unitR.Version
                                )
                            );
                        }
                        break;

                    case UnitReservedDomainEvent:
                        if (entry.Entity is BloodUnit unitRes)
                        {
                            integrationEvents.Add(
                                InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(
                                    unitRes.Id,
                                    unitRes.Status.ToString(),
                                    unitRes.Version
                                )
                            );
                        }
                        break;

                    case UnitReservationReleasedDomainEvent:
                        if (entry.Entity is BloodUnit unitRel)
                        {
                            integrationEvents.Add(
                                InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(
                                    unitRel.Id,
                                    unitRel.Status.ToString(),
                                    unitRel.Version
                                )
                            );
                        }
                        break;

                    case UnitExpiredDomainEvent expired:
                        integrationEvents.Add(
                            InventoryIntegrationEventMapper.ToUnitExpiredEvent(expired)
                        );
                        if (entry.Entity is BloodUnit unitExp)
                        {
                            integrationEvents.Add(
                                InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(
                                    unitExp.Id,
                                    unitExp.Status.ToString(),
                                    unitExp.Version
                                )
                            );
                        }
                        break;

                    case UnitDispatchedDomainEvent:
                        if (entry.Entity is BloodUnit unitDisp)
                        {
                            integrationEvents.Add(
                                InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(
                                    unitDisp.Id,
                                    unitDisp.Status.ToString(),
                                    unitDisp.Version
                                )
                            );
                        }
                        break;

                    case UnitDeliveredDomainEvent:
                        if (entry.Entity is BloodUnit unitDel)
                        {
                            integrationEvents.Add(
                                InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(
                                    unitDel.Id,
                                    unitDel.Status.ToString(),
                                    unitDel.Version
                                )
                            );
                        }
                        break;
                }
            }
        }

        IEventPublisher eventPublisher = _eventPublisherProvider();
        foreach (var evt in integrationEvents)
        {
            await eventPublisher.PublishAsync(evt, cancellationToken);
        }
    }
}
