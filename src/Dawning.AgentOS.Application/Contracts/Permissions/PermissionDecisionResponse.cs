namespace Dawning.AgentOS.Application.Contracts.Permissions;

public sealed record PermissionDecisionResponse(
  string Level,
  bool RequiresConfirmation,
  string Reason
);
