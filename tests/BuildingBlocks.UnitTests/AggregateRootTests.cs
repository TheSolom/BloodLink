using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class AggregateRootTests
{
    private record SampleDomainEvent(Guid EventId, DateTimeOffset OccurredOn, string Note)
        : IDomainEvent;

    private sealed class SampleAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public static void DoSomething(SampleAggregate aggregate, string note) =>
            aggregate.AddDomainEvent(
                new SampleDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, note)
            );
    }

    [Fact]
    public void AddDomainEvent_RecordsEvent()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        SampleAggregate.DoSomething(aggregate, "Initial Event");

        aggregate.DomainEvents.Count.ShouldBe(1);
        var evt = aggregate.DomainEvents.First() as SampleDomainEvent;
        evt.ShouldNotBeNull();
        evt.Note.ShouldBe("Initial Event");
    }

    [Fact]
    public void ClearDomainEvents_ReturnsBufferedEventsAndEmptiesCollection()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        SampleAggregate.DoSomething(aggregate, "Event 1");
        SampleAggregate.DoSomething(aggregate, "Event 2");

        IReadOnlyCollection<IDomainEvent> cleared = aggregate.ClearDomainEvents();

        cleared.Count.ShouldBe(2);
        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AggregateRoot_ImplementsIHasDomainEvents()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        aggregate.ShouldBeAssignableTo<IHasDomainEvents>();
    }

    [Fact]
    public void PolymorphicEventHarvesting_CollectsAndClearsEventsAcrossAggregates()
    {
        var agg1 = new SampleAggregate(Guid.NewGuid());
        var agg2 = new SampleAggregate(Guid.NewGuid());
        SampleAggregate.DoSomething(agg1, "Agg1 Event");
        SampleAggregate.DoSomething(agg2, "Agg2 Event");

        IHasDomainEvents[] entities = [agg1, agg2];
        List<IDomainEvent> allEvents = [];
        foreach (IHasDomainEvents entity in entities)
        {
            allEvents.AddRange(entity.ClearDomainEvents());
        }

        allEvents.Count.ShouldBe(2);
        agg1.DomainEvents.ShouldBeEmpty();
        agg2.DomainEvents.ShouldBeEmpty();
    }
}
