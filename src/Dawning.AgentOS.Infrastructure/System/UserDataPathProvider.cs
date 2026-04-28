using Dawning.AgentOS.Application.Abstractions.System;
using Dawning.AgentOS.Infrastructure.Persistence.Sqlite;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.System;

public sealed class UserDataPathProvider(IOptions<AgentOsStorageOptions> options)
  : IUserDataPathProvider
{
  private const string ApplicationDirectoryName = "DawningAgentOS";

  public string GetUserDataDirectory()
  {
    if (!string.IsNullOrWhiteSpace(options.Value.DataDirectory))
    {
      return Path.GetFullPath(options.Value.DataDirectory);
    }

    if (OperatingSystem.IsMacOS())
    {
      return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        ApplicationDirectoryName
      );
    }

    var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    return Path.Combine(applicationData, ApplicationDirectoryName);
  }
}
