using Dawning.AgentOS.Api.Results;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Memory;
using Microsoft.AspNetCore.Http;

namespace Dawning.AgentOS.Api.Endpoints.Memory;

/// <summary>
/// Registers the memory ledger endpoint group on the application's
/// <see cref="IEndpointRouteBuilder"/>. Per ADR-033 §决策 J1 V0 surfaces
/// five endpoints under <c>/api/memory</c>:
/// <list type="number">
///   <item>
///     <description><c>POST /api/memory</c> — create a user-explicit entry.</description>
///   </item>
///   <item>
///     <description><c>GET  /api/memory</c> — paged list, ordered <c>updated_at DESC, id DESC</c>.</description>
///   </item>
///   <item>
///     <description><c>GET  /api/memory/{id:guid}</c> — fetch a single entry.</description>
///   </item>
///   <item>
///     <description><c>PATCH /api/memory/{id:guid}</c> — partial update (content / scope / sensitivity / status).</description>
///   </item>
///   <item>
///     <description><c>DELETE /api/memory/{id:guid}</c> — soft-delete an entry.</description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Auth is enforced by <see cref="Middleware.StartupTokenMiddleware"/>
/// before routing (ADR-023 §8 / ADR-033 §决策 J1 — startup token only,
/// no per-user identity in V0). For field-level validation failures
/// (codes <c>memory.content.required</c>, <c>memory.scope.tooLong</c>,
/// <c>memory.limit.outOfRange</c>, etc.) the shared
/// <see cref="ResultHttpExtensions.ToHttpResult{T}"/> mapper returns
/// HTTP 400. The two non-field codes need bespoke mapping:
/// <list type="bullet">
///   <item>
///     <description>
///       <c>memory.notFound</c> → <c>404 Not Found</c> (entry lookup miss).
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>memory.invalidStatusTransition</c> → <c>422 Unprocessable Entity</c>
///       (state-machine refused the requested transition).
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>memory.update.empty</c> → <c>400 Bad Request</c> (PATCH body
///       carries no fields; the request shape is malformed).
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class MemoryEndpoints
{
    /// <summary>Per ADR-033 §决策 J1 default page size when the client omits <c>limit</c>.</summary>
    public const int DefaultListLimit = 50;

    /// <summary>Per ADR-033 §决策 J1 default offset when the client omits <c>offset</c>.</summary>
    public const int DefaultListOffset = 0;

    /// <summary>
    /// Maps the memory ledger endpoint group under <c>/api/memory</c>.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="routes"/> is null.</exception>
    public static IEndpointRouteBuilder MapMemoryEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/memory");

        // ADR-033 §决策 J1: POST /api/memory — create user-explicit entry.
        group.MapPost(
            string.Empty,
            async (
                IMemoryLedgerAppService appService,
                CreateMemoryEntryRequest request,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await appService
                    .CreateExplicitAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            }
        );

        // ADR-033 §决策 J1: GET /api/memory — paged list with optional filters.
        group.MapGet(
            string.Empty,
            async (
                IMemoryLedgerAppService appService,
                CancellationToken cancellationToken,
                int? limit,
                int? offset,
                string? status,
                bool? includeSoftDeleted
            ) =>
            {
                var query = new MemoryListQuery(
                    Limit: limit ?? DefaultListLimit,
                    Offset: offset ?? DefaultListOffset,
                    Status: status,
                    IncludeSoftDeleted: includeSoftDeleted ?? false
                );

                var result = await appService
                    .ListAsync(query, cancellationToken)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            }
        );

        // ADR-033 §决策 J1: GET /api/memory/{id:guid} — fetch by id, 404 on miss.
        group.MapGet(
            "/{id:guid}",
            async (
                Guid id,
                IMemoryLedgerAppService appService,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await appService
                    .GetByIdAsync(id, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return (IResult)TypedResults.Ok(result.Value);
                }

                return MapNonFieldError(result.Errors[0]);
            }
        );

        // ADR-033 §决策 J1: PATCH /api/memory/{id:guid} — partial update.
        group.MapPatch(
            "/{id:guid}",
            async (
                Guid id,
                IMemoryLedgerAppService appService,
                UpdateMemoryEntryRequest request,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await appService
                    .UpdateAsync(id, request, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return (IResult)TypedResults.Ok(result.Value);
                }

                var error = result.Errors[0];

                // Field-level validation (e.g. memory.content.required)
                // delegates to the shared mapper (HTTP 400).
                if (!string.IsNullOrWhiteSpace(error.Field))
                {
                    return result.ToHttpResult();
                }

                return MapNonFieldError(error);
            }
        );

        // ADR-033 §决策 J1: DELETE /api/memory/{id:guid} — soft-delete.
        group.MapDelete(
            "/{id:guid}",
            async (
                Guid id,
                IMemoryLedgerAppService appService,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await appService
                    .SoftDeleteAsync(id, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return (IResult)TypedResults.Ok(result.Value);
                }

                return MapNonFieldError(result.Errors[0]);
            }
        );

        return routes;
    }

    /// <summary>
    /// Maps the three non-field <see cref="MemoryErrors"/> codes to
    /// their bespoke HTTP status per ADR-033 §决策 J1. Unknown codes
    /// fall back to HTTP 500 so we surface unexpected errors loudly.
    /// </summary>
    private static IResult MapNonFieldError(Domain.Core.DomainError error)
    {
        var statusCode = error.Code switch
        {
            MemoryErrors.NotFoundCode => StatusCodes.Status404NotFound,
            MemoryErrors.InvalidStatusTransitionCode => StatusCodes.Status422UnprocessableEntity,
            MemoryErrors.UpdateEmptyCode => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        return TypedResults.Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = error.Code,
            }
        );
    }
}
