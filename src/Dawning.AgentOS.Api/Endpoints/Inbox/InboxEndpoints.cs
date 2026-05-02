using Dawning.AgentOS.Api.Results;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Interfaces;

namespace Dawning.AgentOS.Api.Endpoints.Inbox;

/// <summary>
/// Registers the inbox endpoint group on the application's
/// <see cref="IEndpointRouteBuilder"/>. Per ADR-026 §7 V0 surfaces two
/// endpoints under <c>/api/inbox</c>:
/// <list type="number">
///   <item>
///     <description>
///       <c>POST /api/inbox</c> — capture a new item from <see cref="CaptureInboxItemRequest"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>GET  /api/inbox</c> — paged list ordered by capture instant DESC.
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Auth is enforced by <see cref="Middleware.StartupTokenMiddleware"/>
/// before routing (ADR-023 §8 / ADR-026 §J2 — startup token only,
/// no per-user identity in V0). Result→HTTP mapping goes through
/// <see cref="ResultHttpExtensions.ToHttpResult{T}(Domain.Core.Result{T})"/>:
/// success → 200, field-level failure → 400, non-field failure → 422.
/// </remarks>
public static class InboxEndpoints
{
    /// <summary>Per ADR-026 §C2 default page size when the client omits <c>limit</c>.</summary>
    public const int DefaultListLimit = 50;

    /// <summary>Per ADR-026 §C2 default offset when the client omits <c>offset</c>.</summary>
    public const int DefaultListOffset = 0;

    /// <summary>
    /// Maps the inbox endpoint group under <c>/api/inbox</c>.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="routes"/> is null.</exception>
    public static IEndpointRouteBuilder MapInboxEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/inbox");

        group.MapPost(
            string.Empty,
            async (
                IInboxAppService appService,
                CaptureInboxItemRequest request,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await appService
                    .CaptureAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            }
        );

        group.MapGet(
            string.Empty,
            async (
                IInboxAppService appService,
                CancellationToken cancellationToken,
                int? limit,
                int? offset
            ) =>
            {
                var query = new InboxListQuery(
                    Limit: limit ?? DefaultListLimit,
                    Offset: offset ?? DefaultListOffset
                );
                var result = await appService
                    .ListAsync(query, cancellationToken)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            }
        );

        return routes;
    }
}
