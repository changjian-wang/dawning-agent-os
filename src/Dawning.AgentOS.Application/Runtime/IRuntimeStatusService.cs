using Dawning.AgentOS.Application.Contracts.Runtime;

namespace Dawning.AgentOS.Application.Runtime;

public interface IRuntimeStatusService
{
  HealthResponse GetHealth();

  RuntimeStatusResponse GetStatus();
}
