using Dawning.AgentOS.Api.Results;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Interfaces;
using Microsoft.AspNetCore.Http;

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
/// Per ADR-030 a third endpoint is mounted under the same group:
/// <list type="number">
///   <item>
///     <description>
///       <c>POST /api/inbox/items/{id:guid}/summarize</c> — generate a
///       1-3 sentence Chinese summary of the item via the active
///       <c>ILlmProvider</c>; non-idempotent, non-persistent.
///     </description>
///   </item>
/// </list>
/// Per ADR-031 a fourth endpoint joins the group:
/// <list type="number">
///   <item>
///     <description>
///       <c>POST /api/inbox/items/{id:guid}/tags</c> — generate 1-5
///       open-set Chinese tags as a JSON array via the active
///       <c>ILlmProvider</c>; non-idempotent, non-persistent.
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Auth is enforced by <see cref="Middleware.StartupTokenMiddleware"/>
/// before routing (ADR-023 §8 / ADR-026 §J2 — startup token only,
/// no per-user identity in V0). For capture / list, Result→HTTP
/// mapping goes through
/// <see cref="ResultHttpExtensions.ToHttpResult{T}(Domain.Core.Result{T})"/>:
/// success → 200, field-level failure → 400, non-field failure → 422.
/// For summarize / tags, ADR-030 §决策 F1 / ADR-031 §决策 F1 need
/// <c>inbox.notFound</c> → 404, <c>inbox.taggingParseFailed</c> → 422,
/// and <c>llm.*</c> → 401/429/502/400; the endpoints map those manually
/// rather than polluting the shared mapper with route-specific rules.
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

        // ADR-030 §决策 G1: POST /api/inbox/items/{id:guid}/summarize.
        // Manual error mapping per ADR-030 §决策 F1 — inbox.notFound → 404,
        // llm.* → ADR-028 §H1 table.
        group.MapPost(
            "/items/{id:guid}/summarize",
            async (
                Guid id,
                IInboxSummaryAppService summaryAppService,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await summaryAppService
                    .SummarizeAsync(id, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return (IResult)
                        TypedResults.Ok(
                            new InboxItemSummaryResponse(
                                ItemId: result.Value.ItemId,
                                Summary: result.Value.Summary,
                                Model: result.Value.Model,
                                PromptTokens: result.Value.PromptTokens,
                                CompletionTokens: result.Value.CompletionTokens,
                                DurationMs: (long)result.Value.Latency.TotalMilliseconds
                            )
                        );
                }

                var error = result.Errors[0];
                var statusCode = error.Code switch
                {
                    InboxErrors.ItemNotFoundCode => StatusCodes.Status404NotFound,
                    "llm.authenticationFailed" => StatusCodes.Status401Unauthorized,
                    "llm.rateLimited" => StatusCodes.Status429TooManyRequests,
                    "llm.upstreamUnavailable" => StatusCodes.Status502BadGateway,
                    "llm.invalidRequest" => StatusCodes.Status400BadRequest,
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
        );

        // ADR-031 §决策 G1: POST /api/inbox/items/{id:guid}/tags.
        // Manual error mapping per ADR-031 §决策 F1 — inbox.notFound → 404,
        // inbox.taggingParseFailed → 422, llm.* → ADR-028 §H1 table.
        group.MapPost(
            "/items/{id:guid}/tags",
            async (
                Guid id,
                IInboxTaggingAppService taggingAppService,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await taggingAppService
                    .SuggestTagsAsync(id, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return (IResult)
                        TypedResults.Ok(
                            new InboxItemTagsResponse(
                                ItemId: result.Value.ItemId,
                                Tags: result.Value.Tags,
                                Model: result.Value.Model,
                                PromptTokens: result.Value.PromptTokens,
                                CompletionTokens: result.Value.CompletionTokens,
                                DurationMs: (long)result.Value.Latency.TotalMilliseconds
                            )
                        );
                }

                var error = result.Errors[0];
                var statusCode = error.Code switch
                {
                    InboxErrors.ItemNotFoundCode => StatusCodes.Status404NotFound,
                    InboxErrors.TaggingParseFailedCode => StatusCodes.Status422UnprocessableEntity,
                    "llm.authenticationFailed" => StatusCodes.Status401Unauthorized,
                    "llm.rateLimited" => StatusCodes.Status429TooManyRequests,
                    "llm.upstreamUnavailable" => StatusCodes.Status502BadGateway,
                    "llm.invalidRequest" => StatusCodes.Status400BadRequest,
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
        );

        return routes;
    }

    /// <summary>
    /// Response shape for <c>POST /api/inbox/items/{id}/summarize</c>.
    /// Per ADR-030 §决策 B1 the field set mirrors
    /// <c>InboxItemSummary</c>'s record but renames <c>Latency</c> to
    /// <c>DurationMs</c> for wire-side consistency with
    /// <c>/api/llm/ping</c>.
    /// </summary>
    /// <param name="ItemId">The inbox item identifier the summary was produced for.</param>
    /// <param name="Summary">The LLM-generated summary text (1-3 sentences, Chinese).</param>
    /// <param name="Model">Model identifier echoed by the active provider.</param>
    /// <param name="PromptTokens">Tokens consumed by the prompt; <c>null</c> when unreported.</param>
    /// <param name="CompletionTokens">Tokens produced; <c>null</c> when unreported.</param>
    /// <param name="DurationMs">Wall-clock latency in milliseconds.</param>
    private sealed record InboxItemSummaryResponse(
        Guid ItemId,
        string Summary,
        string Model,
        int? PromptTokens,
        int? CompletionTokens,
        long DurationMs
    );

    /// <summary>
    /// Response shape for <c>POST /api/inbox/items/{id}/tags</c>.
    /// Per ADR-031 §决策 B1 the field set mirrors
    /// <c>InboxItemTags</c>'s record but renames <c>Latency</c> to
    /// <c>DurationMs</c> for wire-side consistency with
    /// <c>InboxItemSummaryResponse</c> and <c>/api/llm/ping</c>.
    /// </summary>
    /// <param name="ItemId">The inbox item identifier the tags were produced for.</param>
    /// <param name="Tags">1-5 normalized open-set Chinese tags.</param>
    /// <param name="Model">Model identifier echoed by the active provider.</param>
    /// <param name="PromptTokens">Tokens consumed by the prompt; <c>null</c> when unreported.</param>
    /// <param name="CompletionTokens">Tokens produced; <c>null</c> when unreported.</param>
    /// <param name="DurationMs">Wall-clock latency in milliseconds.</param>
    private sealed record InboxItemTagsResponse(
        Guid ItemId,
        IReadOnlyList<string> Tags,
        string Model,
        int? PromptTokens,
        int? CompletionTokens,
        long DurationMs
    );
}
