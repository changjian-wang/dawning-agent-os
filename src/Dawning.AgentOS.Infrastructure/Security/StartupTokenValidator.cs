using System.Security.Cryptography;
using System.Text;

namespace Dawning.AgentOS.Infrastructure.Security;

public sealed class StartupTokenValidator(IStartupTokenProvider startupTokenProvider)
  : IStartupTokenValidator
{
  public bool IsValid(string? token)
  {
    if (string.IsNullOrWhiteSpace(token))
    {
      return false;
    }

    var expectedBytes = Encoding.UTF8.GetBytes(startupTokenProvider.Token);
    var actualBytes = Encoding.UTF8.GetBytes(token);
    return expectedBytes.Length == actualBytes.Length
      && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
  }
}
