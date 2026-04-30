namespace Dawning.AgentOS.Domain.Core;

/// <summary>
/// Base class for all domain entities, parameterized by identifier type.
/// Provides identity equality semantics and a creation timestamp.
/// </summary>
/// <typeparam name="TId">
/// The identifier type. Must be a non-nullable struct with equality
/// (e.g. <see cref="Guid"/>, <see cref="long"/>).
/// </typeparam>
/// <remarks>
/// <para>
/// <see cref="Id"/> and <see cref="CreatedAt"/> use <c>protected set</c>
/// so concrete entities and their derived rehydration factories can assign
/// them, while external callers cannot mutate identity. New instances assign
/// these values through the parameterized constructor; ORM rehydration assigns
/// them through a <c>Rehydrate</c> static factory on the concrete type.
/// </para>
/// <para>
/// Rehydration must not raise domain events; events represent business actions
/// and loading from a repository is not a business action. <see cref="Entity{TId}"/>
/// itself raises nothing, so this contract is preserved as long as concrete
/// rehydration paths avoid calling business methods.
/// </para>
/// </remarks>
public abstract class Entity<TId>
    : IEquatable<Entity<TId>>
    where TId : struct, IEquatable<TId>
{
    /// <summary>
    /// The entity identifier. Stable for the lifetime of the entity.
    /// Default value (e.g. <see cref="Guid.Empty"/>) indicates an
    /// uninitialised entity and is treated as a programming error.
    /// </summary>
    public TId Id { get; protected set; }

    /// <summary>
    /// UTC creation timestamp. Set once at construction.
    /// </summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>
    /// Initializes a new entity with the given identifier and creation timestamp.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> equals the default value of
    /// <typeparamref name="TId"/>; an uninitialised id is a programming error.
    /// </exception>
    protected Entity(TId id, DateTimeOffset createdAt)
    {
        if (id.Equals(default))
        {
            throw new ArgumentException(
                $"Entity id of type '{typeof(TId).Name}' must not be the default value.",
                nameof(id));
        }

        Id = id;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Parameterless constructor for ORM rehydration.
    /// Concrete aggregates use a <c>Rehydrate</c> static factory that calls
    /// this constructor and assigns <see cref="Id"/> / <see cref="CreatedAt"/>
    /// via <c>protected set</c>. Rehydration must never raise domain events.
    /// </summary>
    protected Entity() { }

    /// <inheritdoc/>
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

        // Default-id entities are considered transient and not equal to anything
        // (including themselves through this comparator).
        if (Id.Equals(default) || other.Id.Equals(default))
        {
            return false;
        }

        return Id.Equals(other.Id);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Entity<TId> e && Equals(e);

    /// <inheritdoc/>
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        => !(left == right);
}
