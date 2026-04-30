namespace Dawning.AgentOS.Domain.Core;

/// <summary>
/// Base class for aggregate roots. Inherits identity semantics from
/// <see cref="Entity{TId}"/> and adds domain event accumulation.
/// </summary>
/// <typeparam name="TId">The identifier type.</typeparam>
/// <remarks>
/// <para>
/// Domain events are accumulated in-memory via <see cref="Raise(IDomainEvent)"/>
/// from inside business methods. They are dispatched by the domain event
/// dispatch behavior in the bus layer after a successful command handler returns.
/// </para>
/// <para>
/// Rehydration via the parameterless constructor path must not raise events.
/// This base class only mutates <see cref="_domainEvents"/> when <see cref="Raise"/>
/// is called explicitly, so the contract holds as long as rehydration factories
/// avoid calling business methods.
/// </para>
/// </remarks>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Read-only snapshot of domain events raised by this aggregate
    /// since construction or the last <see cref="ClearDomainEvents"/> call.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Initializes a new aggregate root with the given identifier and creation timestamp.
    /// </summary>
    protected AggregateRoot(TId id, DateTimeOffset createdAt)
        : base(id, createdAt) { }

    /// <summary>
    /// Parameterless constructor for ORM rehydration; see <see cref="Entity{TId}()"/>.
    /// </summary>
    protected AggregateRoot() { }

    /// <summary>
    /// Records a domain event raised by a business method on this aggregate.
    /// </summary>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears the accumulated domain events. Called by the dispatch behavior
    /// after a successful publish, or by tests after assertions.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
