using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Application service facade for the inbox single-item summarize use
/// case. Per ADR-030 the contract is intentionally narrow: load one
/// item by id, ask the active <c>ILlmProvider</c> for a 1-3 sentence
/// Chinese summary, return the result (or a typed failure). V0 does
/// not persist, cache, batch, or stream.
/// </summary>
public interface IInboxSummaryAppService
{
    /// <summary>
    /// Summarizes the content of an inbox item. Per ADR-030 §决策 D1
    /// the prompt template is fixed in the implementation; callers
    /// cannot override system message, temperature, or token budget
    /// in V0.
    /// </summary>
    /// <param name="itemId">UUIDv7 identifier of the inbox item to summarize.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Success"/> with the summary projection on a
    /// successful LLM round-trip; <see cref="Result{T}.Failure"/> when
    /// the item is missing (<c>inbox.notFound</c>, HTTP 404 in API) or
    /// the LLM provider returned an error (per ADR-028 §H1 mapping —
    /// <c>llm.authenticationFailed</c> → 401, <c>llm.rateLimited</c> →
    /// 429, <c>llm.upstreamUnavailable</c> → 502, <c>llm.invalidRequest</c>
    /// → 400). <see cref="OperationCanceledException"/> from the LLM
    /// call is propagated, not wrapped.
    /// </returns>
    Task<Result<InboxItemSummary>> SummarizeAsync(
        Guid itemId,
        CancellationToken cancellationToken
    );
}
