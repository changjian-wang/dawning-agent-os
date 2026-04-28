using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.Persistence.Sqlite;

public sealed class AgentOsStorageOptions
{
  public const string SectionName = "Storage";

  public string? DataDirectory { get; set; }

  public string DatabaseFileName { get; set; } = "agentos.db";
}

public sealed class AgentOsStorageOptionsValidator : IValidateOptions<AgentOsStorageOptions>
{
  public ValidateOptionsResult Validate(string? name, AgentOsStorageOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    if (string.IsNullOrWhiteSpace(options.DatabaseFileName))
    {
      return ValidateOptionsResult.Fail("Storage:DatabaseFileName is required.");
    }

    if (options.DatabaseFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
    {
      return ValidateOptionsResult.Fail(
        "Storage:DatabaseFileName contains invalid file name characters."
      );
    }

    return ValidateOptionsResult.Success;
  }
}
