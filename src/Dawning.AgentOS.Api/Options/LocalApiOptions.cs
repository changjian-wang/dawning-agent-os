namespace Dawning.AgentOS.Api.Options;

public sealed class LocalApiOptions
{
  public const string SectionName = "LocalApi";

  public string BindHost { get; set; } = "127.0.0.1";
}
