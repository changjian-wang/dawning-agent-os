namespace Dawning.AgentOS.Application.Inbox;

/// <summary>
/// Read-side projection returned by
/// <see cref="Interfaces.IInboxTaggingAppService.SuggestTagsAsync"/>.
/// Per ADR-031 §决策 B1 the field set is intentionally narrow, mirroring
/// <see cref="InboxItemSummary"/>'s shape (ADR-030 §决策 B1):
/// <list type="bullet">
///   <item>
///     <description><see cref="ItemId"/> echoes the request so the
///     renderer can correlate the response with the source row;</description>
///   </item>
///   <item>
///     <description><see cref="Tags"/> is the LLM-generated tag list,
///     post AppService normalization (ADR-031 §决策 D2): trimmed, deduped,
///     length-filtered, capped at 5;</description>
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
/// any persistence id: the result is non-persistent (ADR-031 §决策 E1).
/// </summary>
/// <param name="ItemId">The inbox item identifier the tags were produced for.</param>
/// <param name="Tags">1-5 normalized open-set Chinese tags.</param>
/// <param name="Model">Model identifier echoed by the provider (e.g., <c>"gpt-4.1-2025-04-14"</c>).</param>
/// <param name="PromptTokens">Tokens consumed by the prompt; <c>null</c> when the upstream did not report usage.</param>
/// <param name="CompletionTokens">Tokens produced by the completion; <c>null</c> when the upstream did not report usage.</param>
/// <param name="Latency">Wall-clock latency of the LLM round-trip.</param>
public sealed record InboxItemTags(
    Guid ItemId,
    IReadOnlyList<string> Tags,
    string Model,
    int? PromptTokens,
    int? CompletionTokens,
    TimeSpan Latency
);
