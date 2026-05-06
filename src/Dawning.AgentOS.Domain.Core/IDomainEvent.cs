namespace Dawning.AgentOS.Domain.Core;

/// <summary>
/// Marker interface for domain events.
/// Per ADR-022 this is a plain marker with no external dependency; dispatch
/// is handled by the self-built <c>IDomainEventDispatcher</c> port declared
/// in <c>Application/Abstractions</c>, with its implementation provided by
/// the infrastructure layer.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// UTC timestamp at which the event was raised.
    /// Set by the aggregate at the moment <c>Raise</c> is called;
    /// not modified during dispatch or persistence.
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}
