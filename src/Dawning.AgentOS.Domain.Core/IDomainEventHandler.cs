namespace Dawning.AgentOS.Domain.Core;

/// <summary>
/// Handler contract for a specific <see cref="IDomainEvent"/> type.
/// Per ADR-022 the handler interface lives in <c>Domain.Core</c> alongside
/// <see cref="IDomainEvent"/>; concrete implementations may live in either
/// <c>Application/DomainEventHandlers</c> (for cross-aggregate orchestration)
/// or in the infrastructure layer (for IO side-effects such as audit logs).
/// </summary>
/// <typeparam name="TEvent">
/// The concrete domain event type this handler reacts to. Declared as
/// contravariant (<c>in</c>) so a handler registered for a base event type
/// can react to derived event types as well.
/// </typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Reacts to <paramref name="event"/>. Implementations must respect
    /// <paramref name="cancellationToken"/> and propagate exceptions to the
    /// dispatcher; the dispatcher is responsible for unwrapping reflective
    /// invocation wrappers (e.g. <c>TargetInvocationException</c>) before
    /// re-throwing.
    /// </summary>
    /// <param name="event">The raised domain event instance.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
