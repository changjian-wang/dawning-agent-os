namespace Dawning.AgentOS.Domain.Common;

public abstract record DomainEvent(DateTimeOffset OccurredAt);
