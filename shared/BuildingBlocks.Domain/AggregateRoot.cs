namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Base class for aggregate roots. Aggregate roots manage transactional consistency
/// and publish domain events representing facts that occurred within their boundary.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(TId id) : base(id)
    {
    }

    protected AggregateRoot() : base()
    {
    }

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears and returns all buffered domain events.
    /// Typically invoked by the persistence layer (e.g., DbContext) before or after saving changes.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> ClearDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }
}
