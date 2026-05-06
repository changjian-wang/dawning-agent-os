using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Application service facade for the inbox single-item tagging use
/// case. Per ADR-031 the contract is intentionally narrow: load one
/// item by id, ask the active <c>ILlmProvider</c> for 1-5 open-set
/// Chinese tags as a JSON array, parse and normalize the output, return
/// the result (or a typed failure). V0 does not persist, cache, batch,
/// or stream.
/// </summary>
public interface IInboxTaggingAppService
{
    /// <summary>
    /// Generates an open-set Chinese tag list for an inbox item. Per
    /// ADR-031 §决策 D1 the prompt template is fixed in the implementation;
    /// callers cannot override system message, temperature, token budget,
    /// or expected tag count in V0.
    /// </summary>
    /// <param name="itemId">UUIDv7 identifier of the inbox item to tag.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Success"/> with the tags projection on a
    /// successful LLM round-trip; <see cref="Result{T}.Failure"/> when
    /// the item is missing (<c>inbox.notFound</c>, HTTP 404 in API),
    /// the LLM returned content that could not be parsed into a valid
    /// tag array (<c>inbox.taggingParseFailed</c>, HTTP 422), or the
    /// LLM provider returned an error (per ADR-028 §H1 mapping —
    /// <c>llm.authenticationFailed</c> → 401, <c>llm.rateLimited</c> →
    /// 429, <c>llm.upstreamUnavailable</c> → 502, <c>llm.invalidRequest</c>
    /// → 400). <see cref="OperationCanceledException"/> from the LLM
    /// call is propagated, not wrapped.
    /// </returns>
    Task<Result<InboxItemTags>> SuggestTagsAsync(
        Guid itemId,
        CancellationToken cancellationToken
    );
}
