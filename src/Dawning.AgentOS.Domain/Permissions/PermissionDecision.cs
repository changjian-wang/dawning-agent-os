namespace Dawning.AgentOS.Domain.Permissions;

public sealed record PermissionDecision(ActionLevel Level, bool RequiresConfirmation, string Reason)
{
  public static PermissionDecision Allow(ActionLevel level, string reason)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(reason);
    return new PermissionDecision(level, false, reason);
  }

  public static PermissionDecision RequireConfirmation(ActionLevel level, string reason)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(reason);
    return new PermissionDecision(level, true, reason);
  }
}
