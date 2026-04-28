namespace Dawning.AgentOS.Application.Abstractions.System;

public interface IClock
{
  DateTimeOffset UtcNow { get; }
}
