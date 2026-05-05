using System.Diagnostics;
using System.Text.Json;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Domain.Inbox;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IInboxTaggingAppService"/>. Per
/// ADR-031 the service:
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
///       builds the prompt from a fixed Chinese system message that
///       hard-constrains the output to a JSON array, plus the item
///       content as the user message — V0 does not let callers override
///       either side (ADR-031 §决策 D1);
///     </description>
///   </item>
///   <item>
///     <description>
///       delegates the chat completion to the active <see cref="ILlmProvider"/>
///       and propagates upstream errors verbatim (ADR-031 §决策 F1
///       transcribes ADR-028 §H1's mapping table, untouched);
///     </description>
///   </item>
///   <item>
///     <description>
///       parses the LLM output as a JSON string array and runs
///       defensive normalization (trim, dedup, length filter, cap at 5)
///       per ADR-031 §决策 D2; if the post-normalization list is empty
///       returns <c>inbox.taggingParseFailed</c> (HTTP 422);
///     </description>
///   </item>
///   <item>
///     <description>
///       does not persist, cache, or batch — V0 is per-call, regenerated
///       every time (ADR-031 §决策 E1).
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-031 §决策 H1 the class is registered scoped via the existing
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
public sealed class InboxTaggingAppService(IInboxRepository repository, ILlmProvider llmProvider)
    : IInboxTaggingAppService
{
    /// <summary>
    /// System message prepended to every tagging request (ADR-031 §决策
    /// D1). Chinese, fixed, hard-constrained to a JSON array — the
    /// AppService still defends against mis-formatted output via
    /// <see cref="ParseAndNormalize"/>.
    /// </summary>
    public const string SystemPrompt =
        "你是一个信息整理助手。读取用户提供的材料，输出 1-5 个用于归类的中文短标签。\n"
        + "严格遵循以下规则：\n"
        + "1. 只返回一个 JSON 数组，不要任何前后缀文字、不要 markdown 代码块标记。\n"
        + "2. 数组每个元素是 2-12 字符的中文短词或短词组，不含空格、不含标点。\n"
        + "3. 标签之间语义不重叠（\"机器学习\"和\"深度学习\"任选其一，不同时给）。\n"
        + "4. 优先具体名词或主题词，避免\"信息\"、\"内容\"、\"文本\"这类无信息量的通用词。\n"
        + "示例输出：[\"人工智能\", \"学习方法\", \"效率工具\"]";

    /// <summary>Per ADR-031 §决策 D1 sampling temperature; very low to keep tag sets stable across calls.</summary>
    public const double Temperature = 0.2;

    /// <summary>Per ADR-031 §决策 D1 hard cap on completion tokens; 5 short tags fit comfortably.</summary>
    public const int MaxTokens = 120;

    /// <summary>Per ADR-031 §决策 D2 inclusive maximum number of tags returned to callers.</summary>
    public const int MaxTagCount = 5;

    /// <summary>Per ADR-031 §决策 D1 / §决策 D2 minimum allowed tag length in characters (UTF-16 code units).</summary>
    public const int MinTagLength = 2;

    /// <summary>Per ADR-031 §决策 D1 / §决策 D2 maximum allowed tag length in characters (UTF-16 code units).</summary>
    public const int MaxTagLength = 12;

    private readonly IInboxRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ILlmProvider _llmProvider =
        llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));

    /// <inheritdoc />
    public async Task<Result<InboxItemTags>> SuggestTagsAsync(
        Guid itemId,
        CancellationToken cancellationToken
    )
    {
        var item = await _repository
            .GetByIdAsync(itemId, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return Result<InboxItemTags>.Failure(InboxErrors.ItemNotFound(itemId));
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
            // Per ADR-031 §决策 F1 the LLM error codes (llm.*) pass
            // through unchanged so the API layer can apply the same
            // mapping table as ADR-030 / /api/llm/ping.
            return Result<InboxItemTags>.Failure([.. llmResult.Errors]);
        }

        var completion = llmResult.Value;
        var parseResult = ParseAndNormalize(completion.Content);
        if (!parseResult.IsSuccess)
        {
            return Result<InboxItemTags>.Failure([.. parseResult.Errors]);
        }

        var tags = new InboxItemTags(
            ItemId: item.Id,
            Tags: parseResult.Value,
            Model: completion.Model,
            PromptTokens: completion.PromptTokens,
            CompletionTokens: completion.CompletionTokens,
            Latency: stopwatch.Elapsed
        );

        return Result<InboxItemTags>.Success(tags);
    }

    /// <summary>
    /// Per ADR-031 §决策 D2 the AppService is the last line of defense
    /// against malformed LLM output. The pipeline:
    /// <list type="number">
    ///   <item><description>strip outer whitespace and common code-fence noise (<c>```json</c> / <c>```</c>);</description></item>
    ///   <item><description><c>JsonSerializer.Deserialize&lt;string[]&gt;</c>; failure → <c>inbox.taggingParseFailed</c>;</description></item>
    ///   <item><description>per element: trim, drop nulls, drop empties, drop lengths outside <see cref="MinTagLength"/>..<see cref="MaxTagLength"/>;</description></item>
    ///   <item><description>de-duplicate preserving first occurrence (case-sensitive — Chinese input has no case);</description></item>
    ///   <item><description>cap to <see cref="MaxTagCount"/> elements (<c>Take</c>);</description></item>
    ///   <item><description>if 0 tags survive → <c>inbox.taggingParseFailed</c>.</description></item>
    /// </list>
    /// </summary>
    /// <param name="content">Raw LLM completion content; never null per <see cref="LlmCompletion"/> contract.</param>
    /// <returns>Success with a non-empty list, or failure tagged with <see cref="InboxErrors.TaggingParseFailedCode"/>.</returns>
    internal static Result<IReadOnlyList<string>> ParseAndNormalize(string content)
    {
        var trimmed = StripCodeFence(content).Trim();
        if (trimmed.Length == 0)
        {
            return Result<IReadOnlyList<string>>.Failure(
                InboxErrors.TaggingParseFailed("empty content")
            );
        }

        string[]? raw;
        try
        {
            raw = JsonSerializer.Deserialize<string[]>(trimmed);
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<string>>.Failure(
                InboxErrors.TaggingParseFailed(ex.Message)
            );
        }

        if (raw is null)
        {
            return Result<IReadOnlyList<string>>.Failure(
                InboxErrors.TaggingParseFailed("deserialized to null")
            );
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>(MaxTagCount);
        foreach (var element in raw)
        {
            if (element is null)
            {
                continue;
            }
            var tag = element.Trim();
            if (tag.Length < MinTagLength || tag.Length > MaxTagLength)
            {
                continue;
            }
            if (!seen.Add(tag))
            {
                continue;
            }
            normalized.Add(tag);
            if (normalized.Count >= MaxTagCount)
            {
                break;
            }
        }

        if (normalized.Count == 0)
        {
            return Result<IReadOnlyList<string>>.Failure(
                InboxErrors.TaggingParseFailed("no valid tags after normalization")
            );
        }

        return Result<IReadOnlyList<string>>.Success(normalized);
    }

    /// <summary>
    /// Strips a single leading <c>```json</c> / <c>```</c> fence and the
    /// matching trailing fence if present. LLMs occasionally wrap their
    /// JSON in a fence even when the system prompt explicitly forbids
    /// it; this is a pure surface-level cleanup with no impact on
    /// well-formed inputs.
    /// </summary>
    private static string StripCodeFence(string content)
    {
        var span = content.AsSpan().Trim();
        if (span.Length == 0)
        {
            return string.Empty;
        }

        if (span.StartsWith("```"))
        {
            // Drop everything up to (and including) the next newline,
            // which removes either ``` or ```json (or any other tag).
            var newlineIndex = span.IndexOf('\n');
            if (newlineIndex >= 0)
            {
                span = span[(newlineIndex + 1)..];
            }
            else
            {
                // Single-line fenced content is not valid JSON anyway.
                return string.Empty;
            }
        }

        span = span.TrimEnd();
        if (span.EndsWith("```"))
        {
            span = span[..^3];
        }

        return span.ToString();
    }
}
