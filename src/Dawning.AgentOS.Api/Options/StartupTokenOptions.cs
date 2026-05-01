using System.ComponentModel.DataAnnotations;

namespace Dawning.AgentOS.Api.Options;

/// <summary>
/// Options for the startup-token check enforced by
/// <see cref="Middleware.StartupTokenMiddleware"/>. Per ADR-017 the local
/// backend rejects any request that does not present the per-launch
/// token shared between the Electron shell and the API process.
/// </summary>
/// <remarks>
/// V0 only declares the option shape. Token issuance / rotation / storage
/// is intentionally out of scope here; the middleware reads the value
/// from <see cref="IOptions{StartupTokenOptions}"/> at request time and
/// performs a constant-time comparison against the inbound header. ADR-017
/// validation checklist requires that an empty <see cref="ExpectedToken"/>
/// at startup is treated as an explicit "open mode" (used only by the
/// integration-test factory), and never the default in production.
/// </remarks>
public sealed class StartupTokenOptions
{
    /// <summary>Configuration section name bound to this options class.</summary>
    public const string SectionName = "Api:StartupToken";

    /// <summary>
    /// HTTP header name that carries the startup token. Defaults to
    /// <c>X-Startup-Token</c>.
    /// </summary>
    [Required]
    public string HeaderName { get; init; } = "X-Startup-Token";

    /// <summary>
    /// Expected token value. When empty the middleware treats every
    /// request as authorized; production hosts must always set this
    /// to a per-launch random value.
    /// </summary>
    public string ExpectedToken { get; init; } = string.Empty;
}
