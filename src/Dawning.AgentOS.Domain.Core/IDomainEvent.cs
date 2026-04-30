namespace Dawning.AgentOS.Domain.Core;

/// <summary>
/// Marker interface for domain events.
/// Inherits <see cref="MediatR.INotification"/> so events can be dispatched
/// via <c>IMediator.Publish</c> from a pipeline behavior in the infrastructure
/// layer. Domain.Core depends only on the <c>MediatR.Contracts</c> abstraction
/// package; the main MediatR package must not reach the domain layer.
/// </summary>
public interface IDomainEvent : MediatR.INotification
{
    /// <summary>
    /// UTC timestamp at which the event was raised.
    /// Set by the aggregate at the moment <c>Raise</c> is called;
    /// not modified during dispatch or persistence.
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}
