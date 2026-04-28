using Dawning.AgentOS.Application.Abstractions.System;
using Dawning.AgentOS.Application.Contracts.Permissions;
using Dawning.AgentOS.Application.Contracts.Runtime;
using Dawning.AgentOS.Domain.Permissions;
using Dawning.AgentOS.Domain.Services.Permissions;

namespace Dawning.AgentOS.Application.Runtime;

public sealed class RuntimeStatusService(
  IClock clock,
  IUserDataPathProvider userDataPathProvider,
  IPermissionDecisionService permissionDecisionService
) : IRuntimeStatusService
{
  public HealthResponse GetHealth()
  {
    return new HealthResponse("healthy", clock.UtcNow);
  }

  public RuntimeStatusResponse GetStatus()
  {
    var decision = permissionDecisionService.Decide(ActionLevel.ContentModification);

    return new RuntimeStatusResponse(
      "ready",
      userDataPathProvider.GetUserDataDirectory(),
      new PermissionDecisionResponse(
        decision.Level.ToString(),
        decision.RequiresConfirmation,
        decision.Reason
      ),
      clock.UtcNow
    );
  }
}
