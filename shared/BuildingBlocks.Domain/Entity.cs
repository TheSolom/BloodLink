namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Base class for all entities with an explicit identifier.
/// Entities are compared by their identity and runtime type, not by attributes.
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
    /// Required parameterless constructor for ORM materialization (EF Core).
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Core equality override implementing strict identity validation.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not Entity<TId> other)
        {
            return false;
        }

        return Equals(other);
    }

    public virtual bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Ensure we are dealing with matching unproxied domain types
        if (GetRealType(GetType()) != GetRealType(other.GetType()))
        {
            return false;
        }

        // Transient entities (unsaved, default ID) are never equal to anything else
        if (IsTransient() || other.IsTransient())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <summary>
    /// Complements standard Object.Equals implementation seamlessly.
    /// </summary>
    public override int GetHashCode()
    {
        if (IsTransient())
        {
            return base.GetHashCode();
        }

        return HashCode.Combine(GetRealType(GetType()), Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) =>
        !(left == right);

    private bool IsTransient() => EqualityComparer<TId>.Default.Equals(Id, default!);

    private static Type GetRealType(Type type)
    {
        if (type.Namespace == "Castle.Proxies" ||
            (type.FullName?.StartsWith("Microsoft.EntityFrameworkCore.Proxy", StringComparison.Ordinal) == true))
        {
            return type.BaseType ?? type;
        }

        return type;
    }
}
