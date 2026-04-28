using Dawning.AgentOS.Domain.Permissions;

namespace Dawning.AgentOS.Domain.Services.Permissions;

public sealed class PermissionDecisionService : IPermissionDecisionService
{
  public PermissionDecision Decide(ActionLevel level)
  {
    return level switch
    {
      ActionLevel.Information => PermissionDecision.Allow(
        level,
        "Read-only information can run without confirmation."
      ),
      ActionLevel.ReversibleOrganization => PermissionDecision.Allow(
        level,
        "Reversible organization can run and report the action trail."
      ),
      ActionLevel.ContentModification => PermissionDecision.RequireConfirmation(
        level,
        "Content modification requires explicit user confirmation."
      ),
      ActionLevel.HighRisk => PermissionDecision.RequireConfirmation(
        level,
        "High-risk or irreversible actions require one-click user confirmation."
      ),
      _ => PermissionDecision.RequireConfirmation(
        level,
        "Unknown action levels default to confirmation."
      ),
    };
  }
}
