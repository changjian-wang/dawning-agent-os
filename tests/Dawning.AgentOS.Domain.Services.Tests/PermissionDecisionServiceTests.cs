using Dawning.AgentOS.Domain.Permissions;
using Dawning.AgentOS.Domain.Services.Permissions;

namespace Dawning.AgentOS.Domain.Services.Tests;

public sealed class PermissionDecisionServiceTests
{
  [Theory]
  [InlineData(ActionLevel.Information, false)]
  [InlineData(ActionLevel.ReversibleOrganization, false)]
  [InlineData(ActionLevel.ContentModification, true)]
  [InlineData(ActionLevel.HighRisk, true)]
  public void Decide_matches_action_level_boundary(
    ActionLevel actionLevel,
    bool requiresConfirmation
  )
  {
    var service = new PermissionDecisionService();

    var decision = service.Decide(actionLevel);

    Assert.Equal(actionLevel, decision.Level);
    Assert.Equal(requiresConfirmation, decision.RequiresConfirmation);
    Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
  }
}
