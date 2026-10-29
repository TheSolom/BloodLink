namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Defines an aggregate root in Domain-Driven Design.
/// Aggregate roots act as transactional consistency boundaries and expose domain events.
/// Repositories should only load and persist aggregate roots, never internal child entities directly.
/// </summary>
public interface IAggregateRoot : IHasDomainEvents
{
}

