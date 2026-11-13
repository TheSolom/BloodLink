using BloodLink.Inventory.Api.Infrastructure.Messaging;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests.Messaging;

public class DomainEventOutboxInterceptorTests
{
    private sealed class FakeEventPublisher : IEventPublisher
    {
        public List<object> PublishedMessages { get; } = [];

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
            where T : class
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishBatchAsync<T>(
            IEnumerable<T> messages,
            CancellationToken cancellationToken = default
        )
            where T : class
        {
            PublishedMessages.AddRange(messages);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_WhenDonationReceived_ShouldPublishReceivedAndStatusChangedEvents()
    {
        var fakePublisher = new FakeEventPublisher();
        var interceptor = new DomainEventOutboxInterceptor(fakePublisher);

        DbContextOptions<InventoryDbContext> options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlServer("Server=dummy;Database=dummy;")
                .Options;

        await using var context = new InventoryDbContext(options);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var unit = BloodUnit.ReceiveDonation(
            Guid.NewGuid(),
            DonationNumber.Create("DON-11223344"),
            BloodType.Parse("O+"),
            BloodComponent.RedCells,
            now,
            Guid.NewGuid(),
            now
        );

        unit.DomainEvents.Count.ShouldBeGreaterThan(0);
        context.BloodUnits.Add(unit);

        await interceptor.DispatchDomainEventsAsync(context);

        unit.DomainEvents.ShouldBeEmpty();
        fakePublisher.PublishedMessages.Count.ShouldBe(2);
        fakePublisher.PublishedMessages.ShouldContain(m => m is UnitReceivedIntegrationEvent);
        fakePublisher.PublishedMessages.ShouldContain(m => m is UnitStatusChangedIntegrationEvent);

        UnitStatusChangedIntegrationEvent statusEvent = fakePublisher
            .PublishedMessages.OfType<UnitStatusChangedIntegrationEvent>()
            .Single();
        statusEvent.UnitId.ShouldBe(unit.Id);
        statusEvent.Status.ShouldBe(BloodUnitStatus.Available.ToString());
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_WhenUnitExpired_ShouldPublishExpiredAndStatusChangedEvents()
    {
        var fakePublisher = new FakeEventPublisher();
        var interceptor = new DomainEventOutboxInterceptor(fakePublisher);

        DbContextOptions<InventoryDbContext> options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlServer("Server=dummy;Database=dummy;")
                .Options;

        await using var context = new InventoryDbContext(options);

        DateTimeOffset collectedAt = DateTimeOffset.UtcNow.AddDays(-40);
        DateTimeOffset donationTime = collectedAt.AddHours(1);
        var unit = BloodUnit.ReceiveDonation(
            Guid.NewGuid(),
            DonationNumber.Create("DON-55667788"),
            BloodType.Parse("B-"),
            BloodComponent.RedCells,
            collectedAt,
            Guid.NewGuid(),
            donationTime
        );

        // Clear donation event
        unit.ClearDomainEvents();

        // Expire unit after shelf life
        DateTimeOffset expireTime = collectedAt.AddDays(43);
        unit.Expire(expireTime);

        unit.DomainEvents.Count.ShouldBeGreaterThan(0);
        context.BloodUnits.Add(unit);

        await interceptor.DispatchDomainEventsAsync(context);

        unit.DomainEvents.ShouldBeEmpty();
        fakePublisher.PublishedMessages.Count.ShouldBe(2);
        fakePublisher.PublishedMessages.ShouldContain(m => m is UnitExpiredIntegrationEvent);
        fakePublisher.PublishedMessages.ShouldContain(m => m is UnitStatusChangedIntegrationEvent);

        UnitExpiredIntegrationEvent expiredEvent = fakePublisher
            .PublishedMessages.OfType<UnitExpiredIntegrationEvent>()
            .Single();
        expiredEvent.UnitId.ShouldBe(unit.Id);

        UnitStatusChangedIntegrationEvent statusEvent = fakePublisher
            .PublishedMessages.OfType<UnitStatusChangedIntegrationEvent>()
            .Single();
        statusEvent.Status.ShouldBe(BloodUnitStatus.Expired.ToString());
    }
}
