using Dawning.AgentOS.Application.Runtime;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Application service facade for runtime-status use cases. Per ADR-022
/// this is the contract the API layer depends on; implementations live in
/// <c>Application/Services</c> and are registered as scoped services via
/// the cross-cutting IoC composition root.
/// </summary>
public interface IRuntimeAppService
{
    /// <summary>
    /// Returns a snapshot of the local backend process's runtime health.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> wrapping a <see cref="RuntimeStatus"/>
    /// snapshot. V0 always succeeds with <c>Healthy = true</c>; future
    /// slices may surface probe failures as <see cref="Result{T}.Failure"/>.
    /// </returns>
    Task<Result<RuntimeStatus>> GetStatusAsync(CancellationToken cancellationToken);
}
