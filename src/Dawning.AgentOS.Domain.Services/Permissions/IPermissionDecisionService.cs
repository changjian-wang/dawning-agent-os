using Dawning.AgentOS.Domain.Permissions;

namespace Dawning.AgentOS.Domain.Services.Permissions;

public interface IPermissionDecisionService
{
  PermissionDecision Decide(ActionLevel level);
}
