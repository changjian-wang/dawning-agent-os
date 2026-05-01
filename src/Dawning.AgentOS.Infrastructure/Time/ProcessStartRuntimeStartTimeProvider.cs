using Dawning.AgentOS.Application.Abstractions;

namespace Dawning.AgentOS.Infrastructure.Time;

/// <summary>
/// Default <see cref="IRuntimeStartTimeProvider"/> implementation that
/// captures the start instant once at construction. Registered as a
/// singleton; the captured value never changes for the lifetime of the
/// host.
/// </summary>
/// <remarks>
/// V0 uses the moment the singleton is built rather than the OS process
/// start time. This is a deliberate simplification: the abstraction's
/// contract (<see cref="IRuntimeStartTimeProvider.StartedAtUtc"/>) is
/// "fixed instant per process lifetime", which holds either way.
/// Switching to <c>System.Diagnostics.Process.GetCurrentProcess().StartTime</c>
/// is a one-line change deferred until something actually needs the OS
/// boot moment.
/// </remarks>
public sealed class ProcessStartRuntimeStartTimeProvider : IRuntimeStartTimeProvider
{
    /// <summary>Initializes the provider, capturing the current UTC instant.</summary>
    public ProcessStartRuntimeStartTimeProvider()
    {
        StartedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public DateTimeOffset StartedAtUtc { get; }
}
