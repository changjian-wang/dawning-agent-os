namespace Dawning.AgentOS.Domain.Core;

/// <summary>
/// Represents a single business-level error, optionally tied to a request field.
/// </summary>
/// <param name="Code">Stable, machine-readable error code (e.g. <c>"runtime.checkpoint.duplicate"</c>).</param>
/// <param name="Message">Human-readable message; UI may localize via <see cref="Code"/>.</param>
/// <param name="Field">Optional field name for input-validation errors; <c>null</c> for non-field errors.</param>
public sealed record DomainError(string Code, string Message, string? Field = null);
