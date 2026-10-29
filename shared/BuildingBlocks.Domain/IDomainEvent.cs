namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Marker interface for in-process domain events raised by aggregate roots.
/// Domain events represent facts that have occurred within the domain boundary.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}
