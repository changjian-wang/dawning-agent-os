using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Runtime;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IRuntimeAppService"/>. Composes the
/// runtime start-time provider with the clock and reports an always-healthy
/// snapshot in V0.
/// </summary>
/// <remarks>
/// Per ADR-022 this service is the canonical reference for the AppService
/// facade pattern: API controllers depend on <see cref="IRuntimeAppService"/>
/// (declared in <c>Application/Interfaces</c>) rather than dispatching
/// messages through a mediator. Cross-cutting concerns (logging,
/// transaction, validation) are handled inside the AppService method body
/// or via decorators, never via pipeline behaviors.
/// </remarks>
public sealed class RuntimeAppService(IClock clock, IRuntimeStartTimeProvider startTimeProvider)
    : IRuntimeAppService
{
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IRuntimeStartTimeProvider _startTimeProvider =
        startTimeProvider ?? throw new ArgumentNullException(nameof(startTimeProvider));

    /// <inheritdoc />
    public Task<Result<RuntimeStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var startedAt = _startTimeProvider.StartedAtUtc;
        var now = _clock.UtcNow;
        var uptime = now >= startedAt ? now - startedAt : TimeSpan.Zero;

        var snapshot = new RuntimeStatus(
            StartedAtUtc: startedAt,
            NowUtc: now,
            Uptime: uptime,
            Healthy: true
        );

        return Task.FromResult(Result<RuntimeStatus>.Success(snapshot));
    }
}
