using Dawning.AgentOS.Application.Abstractions.System;
using Dawning.AgentOS.Application.Runtime;
using Dawning.AgentOS.Domain.Services.Permissions;

namespace Dawning.AgentOS.Application.Tests;

public sealed class RuntimeStatusServiceTests
{
  [Fact]
  public void GetStatus_returns_runtime_contract_without_domain_entity_exposure()
  {
    var clock = new FixedClock(DateTimeOffset.Parse("2026-04-28T00:00:00Z"));
    var pathProvider = new FixedUserDataPathProvider(
      "C:\\Users\\agent\\AppData\\Roaming\\DawningAgentOS"
    );
    var service = new RuntimeStatusService(clock, pathProvider, new PermissionDecisionService());

    var status = service.GetStatus();

    Assert.Equal("ready", status.Status);
    Assert.Equal(pathProvider.Directory, status.DataDirectory);
    Assert.True(status.ContentModificationDecision.RequiresConfirmation);
    Assert.Equal(clock.UtcNow, status.ServerTimeUtc);
  }

  private sealed class FixedClock(DateTimeOffset utcNow) : IClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }

  private sealed class FixedUserDataPathProvider(string directory) : IUserDataPathProvider
  {
    public string Directory { get; } = directory;

    public string GetUserDataDirectory()
    {
      return Directory;
    }
  }
}
