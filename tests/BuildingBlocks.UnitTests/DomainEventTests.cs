using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class DomainEventTests
{
    private sealed record TestDomainEvent(string Payload) : DomainEvent;

    [Fact]
    public void DomainEvent_ImplementsIDomainEvent()
    {
        var evt = new TestDomainEvent("BloodUnitReserved");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void DomainEvent_InitializesEventIdAndUtcTimestampByDefault()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        var evt = new TestDomainEvent("PayloadA");
        DateTimeOffset after = DateTimeOffset.UtcNow;

        evt.EventId.ShouldNotBe(Guid.Empty);
        // Verify UUID version is 7 (.NET 9+ Guid.CreateVersion7())
        evt.EventId.Version.ShouldBe(7);

        evt.OccurredOn.ShouldBeGreaterThanOrEqualTo(before);
        evt.OccurredOn.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void DomainEvent_ConsecutiveInstances_HaveDistinctEventIds()
    {
        var evt1 = new TestDomainEvent("Payload1");
        var evt2 = new TestDomainEvent("Payload2");

        evt1.EventId.ShouldNotBe(evt2.EventId);
    }

    [Fact]
    public void DomainEvent_SupportsNonDestructiveMutation()
    {
        var original = new TestDomainEvent("Original");
        TestDomainEvent modified = original with { Payload = "Modified" };

        modified.Payload.ShouldBe("Modified");
        modified.EventId.ShouldBe(original.EventId);
        modified.OccurredOn.ShouldBe(original.OccurredOn);
    }
}

