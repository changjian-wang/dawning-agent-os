namespace Dawning.AgentOS.Infrastructure.Security;

public interface IStartupTokenValidator
{
  bool IsValid(string? token);
}
