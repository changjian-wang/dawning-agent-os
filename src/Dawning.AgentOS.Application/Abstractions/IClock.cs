namespace Dawning.AgentOS.Application.Abstractions;

/// <summary>
/// Provides the current point in time. Use cases that need a clock must take
/// this port and never call <see cref="DateTimeOffset.UtcNow"/> directly so
/// that handlers stay deterministic under test.
/// </summary>
/// <remarks>
/// Implementations live in <c>Dawning.AgentOS.Infra.*</c> projects; this
/// declaration sits under <c>Application/Abstractions/</c> per ADR-021.
/// </remarks>
public interface IClock
{
    /// <summary>The current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }
}
