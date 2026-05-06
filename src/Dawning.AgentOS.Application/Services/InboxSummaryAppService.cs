using System.Diagnostics;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Domain.Inbox;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IInboxSummaryAppService"/>. Per
/// ADR-030 the service:
/// <list type="number">
///   <item>
///     <description>
///       loads the source aggregate via <see cref="IInboxRepository.GetByIdAsync"/>;
///       absence becomes <c>inbox.notFound</c> (HTTP 404 in the API
///       layer);
///     </description>
///   </item>
///   <item>
///     <description>
///       builds the prompt from a fixed Chinese system message plus the
///       item content as the user message — V0 does not let callers
///       override either side (ADR-030 §决策 D1);
///     </description>
///   </item>
///   <item>
///     <description>
///       delegates the chat completion to the active <see cref="ILlmProvider"/>
///       and propagates upstream errors verbatim (ADR-030 §决策 F1
///       transcribes ADR-028 §H1's mapping table, untouched);
///     </description>
///   </item>
///   <item>
///     <description>
///       does not persist, cache, or batch — V0 is per-call, regenerated
///       every time (ADR-030 §决策 E1).
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-030 §决策 H1 the class is registered scoped via the existing
/// <c>AddApplication()</c> reflection-based scan in
/// <c>ApplicationServiceCollectionExtensions</c>; no manual DI line is
/// required because the class follows the <c>IXxxAppService</c> /
/// <c>XxxAppService</c> naming convention.
/// </para>
/// <para>
/// <see cref="OperationCanceledException"/> from the LLM call is
/// propagated, mirroring <see cref="ILlmProvider.CompleteAsync"/>'s own
/// contract (ADR-028 §H1). Callers that need a typed cancellation are
/// expected to surface their own.
/// </para>
/// </remarks>
public sealed class InboxSummaryAppService(IInboxRepository repository, ILlmProvider llmProvider)
    : IInboxSummaryAppService
{
    /// <summary>
    /// System message prepended to every summary request (ADR-030 §决策
    /// D1). Chinese, fixed, no markdown — the renderer renders the
    /// model's output verbatim.
    /// </summary>
    public const string SystemPrompt =
        "你是一个信息整理助手。用 1-3 句中文总结用户提供的材料的核心要点。"
        + "直接返回总结正文，不要前缀（如\"总结：\"）、不要 markdown 标记、不要解释你做了什么。";

    /// <summary>Per ADR-030 §决策 D1 sampling temperature; low to keep summaries stable.</summary>
    public const double Temperature = 0.3;

    /// <summary>Per ADR-030 §决策 D1 hard cap on completion tokens; 1-3 Chinese sentences fit.</summary>
    public const int MaxTokens = 200;

    private readonly IInboxRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ILlmProvider _llmProvider =
        llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));

    /// <inheritdoc />
    public async Task<Result<InboxItemSummary>> SummarizeAsync(
        Guid itemId,
        CancellationToken cancellationToken
    )
    {
        var item = await _repository
            .GetByIdAsync(itemId, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return Result<InboxItemSummary>.Failure(InboxErrors.ItemNotFound(itemId));
        }

        var request = new LlmRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, SystemPrompt),
                new LlmMessage(LlmRole.User, item.Content),
            ],
            Model: null,
            Temperature: Temperature,
            MaxTokens: MaxTokens
        );

        var stopwatch = Stopwatch.StartNew();
        var llmResult = await _llmProvider
            .CompleteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        if (!llmResult.IsSuccess)
        {
            // Per ADR-030 §决策 F1 the LLM error codes (llm.*) pass
            // through unchanged so the API layer can apply the same
            // mapping table as /api/llm/ping.
            return Result<InboxItemSummary>.Failure([.. llmResult.Errors]);
        }

        var completion = llmResult.Value;
        var summary = new InboxItemSummary(
            ItemId: item.Id,
            Summary: completion.Content,
            Model: completion.Model,
            PromptTokens: completion.PromptTokens,
            CompletionTokens: completion.CompletionTokens,
            Latency: stopwatch.Elapsed
        );

        return Result<InboxItemSummary>.Success(summary);
    }
}
