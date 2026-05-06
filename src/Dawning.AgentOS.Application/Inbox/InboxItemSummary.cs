namespace Dawning.AgentOS.Application.Inbox;

/// <summary>
/// Read-side projection returned by
/// <see cref="Interfaces.IInboxSummaryAppService.SummarizeAsync"/>. Per
/// ADR-030 §决策 B1 the field set is intentionally narrow:
/// <list type="bullet">
///   <item>
///     <description><see cref="ItemId"/> echoes the request so the
///     renderer can correlate the response with the source row;</description>
///   </item>
///   <item>
///     <description><see cref="Summary"/> is the LLM-generated 1-3 sentence
///     summary, returned verbatim (no markdown, no prefix);</description>
///   </item>
///   <item>
///     <description><see cref="Model"/> echoes the model identifier the
///     active provider reported, so dogfood logs can tell which model
///     produced the output;</description>
///   </item>
///   <item>
///     <description><see cref="PromptTokens"/> / <see cref="CompletionTokens"/>
///     surface upstream usage when reported (null otherwise) for
///     cost / latency observability;</description>
///   </item>
///   <item>
///     <description><see cref="Latency"/> is the wall-clock duration of
///     the LLM round-trip including local overhead.</description>
///   </item>
/// </list>
/// V0 deliberately omits <c>generatedAtUtc</c>, <c>provider</c>, and
/// any persistence id: the result is non-persistent (ADR-030 §决策 E1)
/// so there is nothing to surface beyond what the LLM call itself
/// produced.
/// </summary>
/// <param name="ItemId">The inbox item identifier the summary was produced for.</param>
/// <param name="Summary">The LLM-generated summary text (1-3 sentences, Chinese).</param>
/// <param name="Model">Model identifier echoed by the provider (e.g., <c>"gpt-4.1-2025-04-14"</c>).</param>
/// <param name="PromptTokens">Tokens consumed by the prompt; <c>null</c> when the upstream did not report usage.</param>
/// <param name="CompletionTokens">Tokens produced by the completion; <c>null</c> when the upstream did not report usage.</param>
/// <param name="Latency">Wall-clock latency of the LLM round-trip.</param>
public sealed record InboxItemSummary(
    Guid ItemId,
    string Summary,
    string Model,
    int? PromptTokens,
    int? CompletionTokens,
    TimeSpan Latency
);
