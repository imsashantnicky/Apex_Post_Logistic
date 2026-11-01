namespace ApexPost.Domain.Common;

/// <summary>Base for objects with an identity: two entities are equal when their type and Id match.</summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        if (id is null)
        {
            throw new DomainException("An id is required.");
        }

        Id = id;
    }

    public TId Id { get; }

    /// <summary>Version of the stored state this entity was loaded from, used for optimistic concurrency.</summary>
    public int Version { get; init; }

    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
