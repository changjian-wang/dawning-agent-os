using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Dawning.AgentOS.Infrastructure.Security;

public sealed class StartupTokenProvider(IConfiguration configuration) : IStartupTokenProvider
{
  public string Token { get; } = ResolveToken(configuration);

  private static string ResolveToken(IConfiguration configuration)
  {
    var configuredToken = configuration["StartupToken:Token"];
    if (!string.IsNullOrWhiteSpace(configuredToken))
    {
      return configuredToken;
    }

    var environmentToken = Environment.GetEnvironmentVariable("DAWNING_AGENT_OS_STARTUP_TOKEN");
    return !string.IsNullOrWhiteSpace(environmentToken)
      ? environmentToken
      : Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
  }
}
