namespace Dawning.AgentOS.Application.Runtime;

/// <summary>
/// Snapshot of the local backend process's runtime health.
/// </summary>
/// <param name="StartedAtUtc">The UTC instant the process started.</param>
/// <param name="NowUtc">The UTC instant when the snapshot was taken.</param>
/// <param name="Uptime">
/// Wall-clock duration since <paramref name="StartedAtUtc"/>. Always
/// non-negative; the runtime AppService clamps to zero if a clock skew
/// makes <paramref name="NowUtc"/> precede <paramref name="StartedAtUtc"/>.
/// </param>
/// <param name="Healthy">
/// <c>true</c> when the runtime considers itself ready to serve requests.
/// V0 always returns <c>true</c>; later slices may add probes (DB, IPC, etc.).
/// </param>
public sealed record RuntimeStatus(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset NowUtc,
    TimeSpan Uptime,
    bool Healthy
);
