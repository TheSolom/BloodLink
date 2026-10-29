namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Exposes and manages in-memory domain events raised by an entity or aggregate root.
/// Provides a non-generic contract for persistence layers (e.g., EF Core interceptors or Outbox dispatchers)
/// to collect and clear domain events without coupling to a specific entity or identifier type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears and returns all buffered domain events.
    /// Typically invoked by the persistence layer before or after saving changes.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> ClearDomainEvents();
}

