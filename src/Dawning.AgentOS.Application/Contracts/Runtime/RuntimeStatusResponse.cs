using Dawning.AgentOS.Application.Contracts.Permissions;

namespace Dawning.AgentOS.Application.Contracts.Runtime;

public sealed record RuntimeStatusResponse(
  string Status,
  string DataDirectory,
  PermissionDecisionResponse ContentModificationDecision,
  DateTimeOffset ServerTimeUtc
);
