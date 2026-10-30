namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Base class for domain entities with explicit identity.
/// Entities are compared by their runtime type and identifier.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    /// <summary>
    /// Required for EF Core materialization.
    /// </summary>
    protected Entity() { }

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

        if (GetRealType(GetType()) != GetRealType(other.GetType()))
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

    public override int GetHashCode() =>
        // Stable throughout the entity lifecycle.
        GetRealType(GetType()).GetHashCode();

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);

    private bool IsTransient() => EqualityComparer<TId>.Default.Equals(Id, default!);

    private static Type GetRealType(Type type) =>
        type.Namespace == "Castle.Proxies" ? type.BaseType ?? type : type;
}

