namespace Dawning.AgentOS.Application.Abstractions;

/// <summary>
/// Reports the moment the local backend process started. Used to compute
/// process uptime in <see cref="Runtime.GetRuntimeStatusQueryHandler"/> and
/// any other diagnostics surface.
/// </summary>
/// <remarks>
/// <para>
/// Implementations live in <c>Dawning.AgentOS.Infra.*</c> projects; this
/// declaration sits under <c>Application/Abstractions/</c> per ADR-021.
/// </para>
/// <para>
/// The contract returns a fixed instant per process lifetime. Implementations
/// must capture the start time once at construction and return the same value
/// for every subsequent call.
/// </para>
/// </remarks>
public interface IRuntimeStartTimeProvider
{
    /// <summary>The UTC instant the process was considered started.</summary>
    DateTimeOffset StartedAtUtc { get; }
}
