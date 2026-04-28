using Dawning.AgentOS.Application.Abstractions.System;

namespace Dawning.AgentOS.Infrastructure.Diagnostics;

public sealed class DiagnosticPathProvider(IUserDataPathProvider userDataPathProvider)
{
  public string GetLogDirectory()
  {
    return Path.Combine(userDataPathProvider.GetUserDataDirectory(), "logs");
  }
}
