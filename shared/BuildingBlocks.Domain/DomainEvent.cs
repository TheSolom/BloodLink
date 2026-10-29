namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Base record for in-process domain events.
/// Provides default initialization of a time-ordered UUIDv7 identifier and UTC timestamp.
/// In modern C#, records provide immutability, structural equality, and non-destructive mutation ('with').
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}

