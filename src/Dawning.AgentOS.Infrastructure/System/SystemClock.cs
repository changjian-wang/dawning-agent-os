using Dawning.AgentOS.Application.Abstractions.System;

namespace Dawning.AgentOS.Infrastructure.System;

public sealed class SystemClock : IClock
{
  public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
