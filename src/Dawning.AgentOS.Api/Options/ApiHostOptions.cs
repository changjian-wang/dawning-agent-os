namespace Dawning.AgentOS.Api.Options;

/// <summary>
/// Top-level options for the local API host. Bound from configuration
/// section <c>Api</c>; the V0 source of truth is <c>appsettings.json</c>
/// in development and the launch-time configuration produced by the
/// Electron shell in production (per ADR-017).
/// </summary>
/// <remarks>
/// Options classes per ADR-023 §1 live under <c>Api/Options/</c>; only
/// the smallest set required for V0 is declared here. Future endpoints
/// add their own options types alongside this file.
/// </remarks>
public sealed class ApiHostOptions
{
    /// <summary>Configuration section name bound to this options class.</summary>
    public const string SectionName = "Api";
}
