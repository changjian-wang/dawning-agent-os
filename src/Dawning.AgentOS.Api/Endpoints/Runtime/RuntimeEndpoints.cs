using Dawning.AgentOS.Api.Results;
using Dawning.AgentOS.Application.Interfaces;

namespace Dawning.AgentOS.Api.Endpoints.Runtime;

/// <summary>
/// Registers the runtime-status endpoint group on the application's
/// <see cref="IEndpointRouteBuilder"/>. Per ADR-023 §2 each AppService
/// contract maps to a single static endpoint class via an extension
/// method named <c>Map&lt;Feature&gt;Endpoints</c>.
/// </summary>
/// <remarks>
/// V0 exposes a single endpoint <c>GET /api/runtime/status</c> backed by
/// <see cref="IRuntimeAppService.GetStatusAsync"/>. Per ADR-023 §7 V0
/// deliberately does not register an <c>IHealthCheck</c>-protocol endpoint
/// at <c>/health</c>; this endpoint is the project's runtime-health
/// surface for the local Electron client.
/// </remarks>
public static class RuntimeEndpoints
{
    /// <summary>
    /// Maps the runtime-status endpoint group under <c>/api/runtime</c>.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="routes"/> is null.</exception>
    public static IEndpointRouteBuilder MapRuntimeEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/runtime");

        group.MapGet(
            "/status",
            async (IRuntimeAppService appService, CancellationToken cancellationToken) =>
            {
                var result = await appService.GetStatusAsync(cancellationToken);
                return result.ToHttpResult();
            }
        );

        return routes;
    }
}
