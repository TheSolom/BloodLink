namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Base class for domain entities with explicit identity.
/// Entities are compared by their runtime type and identifier.
/// </summary>
public abstract class Entity<TId>(TId id) : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected init; } = id ?? throw new ArgumentNullException(nameof(id));

    /// <summary>
    /// Required for EF Core materialization.
    /// </summary>
    protected Entity()
        : this(default!) { }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        if (IsTransient() || other.IsTransient())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);

    private bool IsTransient() => EqualityComparer<TId>.Default.Equals(Id, default!);
}

