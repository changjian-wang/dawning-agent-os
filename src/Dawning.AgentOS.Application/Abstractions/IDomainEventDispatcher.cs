using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Abstractions;

/// <summary>
/// Port for dispatching domain events to their registered handlers.
/// Per ADR-022 this replaces the MediatR-based dispatch path; the
/// concrete implementation lives in the infrastructure layer and is
/// expected to:
/// <list type="bullet">
///   <item>Resolve <see cref="IDomainEventHandler{TEvent}"/> instances
///     from the DI container at dispatch time (scoped lifetime aligns
///     with the request).</item>
///   <item>Invoke handlers serially in registration order.</item>
///   <item>Unwrap reflective invocation wrappers (e.g.
///     <c>System.Reflection.TargetInvocationException</c>) before
///     re-throwing, so the original handler exception and stack trace
///     are preserved.</item>
///   <item>Honor the supplied <see cref="CancellationToken"/> between
///     handler invocations.</item>
/// </list>
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches a single domain event to all registered handlers.
    /// </summary>
    /// <param name="event">The event to dispatch.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken);

    /// <summary>
    /// Dispatches a sequence of domain events. Implementations must
    /// preserve order and apply the same cancellation / exception
    /// semantics as the single-event overload.
    /// </summary>
    /// <param name="events">The events to dispatch in order.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken);
}
