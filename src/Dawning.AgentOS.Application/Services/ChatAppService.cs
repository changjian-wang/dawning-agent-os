using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Chat;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Chat;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IChatAppService"/>. Per
/// ADR-032 §决策 J1 the V0 service is responsible for:
/// <list type="number">
///   <item>
///     <description>
///       Validating user input (content non-empty, content ≤
///       <see cref="ChatMessage.MaxContentLength"/>) and surfacing
///       failures as field-level
///       <see cref="Result.Failure(DomainError[])"/> outcomes.
///     </description>
///   </item>
///   <item>
///     <description>
///       Loading the existing session and its persisted message
///       history; emitting <c>chat.sessionNotFound</c> for missing ids.
///     </description>
///   </item>
///   <item>
///     <description>
///       Persisting the user turn before the LLM call so a mid-stream
///       failure or a process crash still leaves a recoverable trail.
///     </description>
///   </item>
///   <item>
///     <description>
///       Constructing the per-call message list as: [built-in system
///       prompt] + [persisted history in send order] + [the new user
///       turn]. The system prompt is a private constant; clients
///       cannot override it.
///     </description>
///   </item>
///   <item>
///     <description>
///       Forwarding each <see cref="LlmStreamChunk"/> from
///       <see cref="ILlmProvider"/> to the caller, accumulating the
///       text deltas, and persisting the assistant turn after the
///       terminal <see cref="LlmStreamChunkKind.Done"/> chunk arrives.
///       Mid-stream errors propagate to the caller and skip persistence.
///     </description>
///   </item>
///   <item>
///     <description>
///       Setting <see cref="ChatSession.Title"/> from the first user
///       turn and bumping <see cref="ChatSession.UpdatedAt"/> after each
///       message is appended.
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class ChatAppService(
    IClock clock,
    IChatSessionRepository repository,
    ILlmProvider llmProvider
) : IChatAppService
{
    /// <summary>Per ADR-032 §决策 G1 lower bound on the list page size.</summary>
    public const int MinListLimit = 1;

    /// <summary>Per ADR-032 §决策 G1 upper bound on the list page size.</summary>
    public const int MaxListLimit = 200;

    /// <summary>
    /// Per ADR-032 §决策 E1 / J1 the built-in system prompt. The
    /// AppService prepends this verbatim to every chat call; clients
    /// cannot override or read it. Updating the prompt is a code change
    /// and goes through ADR review.
    /// </summary>
    public const string SystemPrompt =
        "你是 dawning-agent-os，主人的个人 AI 管家。你的角色是客体，主人是主体。"
        + "用简洁、克制、专业的中文回答。先听清主人的需求，再给出建议；"
        + "不知道就直说不知道，不要编造事实。";

    /// <summary>Per ADR-032 §决策 J1 sampling temperature; matches inbox-side defaults so the same provider config works.</summary>
    public const double Temperature = 0.7;

    /// <summary>Per ADR-032 §决策 J1 maximum completion tokens; cap is generous for V0 conversational use.</summary>
    public const int MaxTokens = 1024;

    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IChatSessionRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ILlmProvider _llmProvider =
        llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));

    /// <inheritdoc />
    public async Task<Result<ChatSessionDto>> CreateSessionAsync(
        CancellationToken cancellationToken
    )
    {
        var session = ChatSession.Create(_clock.UtcNow);
        await _repository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        return Result<ChatSessionDto>.Success(ToDto(session));
    }

    /// <inheritdoc />
    public async Task<Result<ChatSessionListPage>> ListSessionsAsync(
        ChatSessionListQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var validationErrors = new List<DomainError>();
        if (query.Limit < MinListLimit || query.Limit > MaxListLimit)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "chat.limit.outOfRange",
                    Message: $"limit must be between {MinListLimit} and {MaxListLimit}.",
                    Field: "limit"
                )
            );
        }

        if (query.Offset < 0)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "chat.offset.outOfRange",
                    Message: "offset must be greater than or equal to 0.",
                    Field: "offset"
                )
            );
        }

        if (validationErrors.Count > 0)
        {
            return Result<ChatSessionListPage>.Failure([.. validationErrors]);
        }

        var sessions = await _repository
            .ListAsync(query.Limit, query.Offset, cancellationToken)
            .ConfigureAwait(false);

        var dtos = ImmutableArray.CreateBuilder<ChatSessionDto>(sessions.Count);
        foreach (var session in sessions)
        {
            dtos.Add(ToDto(session));
        }

        return Result<ChatSessionListPage>.Success(
            new ChatSessionListPage(
                Items: dtos.ToImmutable(),
                Limit: query.Limit,
                Offset: query.Offset
            )
        );
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ChatMessageDto>>> ListMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var session = await _repository
            .GetAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return Result<IReadOnlyList<ChatMessageDto>>.Failure(
                ChatErrors.SessionNotFound(sessionId)
            );
        }

        var messages = await _repository
            .LoadMessagesAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);

        var dtos = new List<ChatMessageDto>(messages.Count);
        foreach (var msg in messages)
        {
            dtos.Add(ToDto(msg));
        }

        return Result<IReadOnlyList<ChatMessageDto>>.Success(dtos);
    }

    /// <inheritdoc />
    public async Task<Result<IAsyncEnumerable<LlmStreamChunk>>> SendMessageStreamAsync(
        Guid sessionId,
        SendMessageRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        // Field-level validation runs first so the API layer can map
        // failures to HTTP 400 before any streaming begins.
        var validationErrors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            validationErrors.Add(
                new DomainError(
                    Code: "chat.content.required",
                    Message: "Chat content is required and must not be whitespace.",
                    Field: "content"
                )
            );
        }
        else if (request.Content.Length > ChatMessage.MaxContentLength)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "chat.content.tooLong",
                    Message: $"Chat content must be {ChatMessage.MaxContentLength} characters or fewer.",
                    Field: "content"
                )
            );
        }

        if (validationErrors.Count > 0)
        {
            return Result<IAsyncEnumerable<LlmStreamChunk>>.Failure([.. validationErrors]);
        }

        var session = await _repository
            .GetAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return Result<IAsyncEnumerable<LlmStreamChunk>>.Failure(
                ChatErrors.SessionNotFound(sessionId)
            );
        }

        // Load persisted history in send order before we append the new
        // user turn — gives us a clean snapshot to feed the LLM.
        var history = await _repository
            .LoadMessagesAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);

        var capturedAt = _clock.UtcNow;
        var userMessage = ChatMessage.CreateUser(sessionId, request.Content, capturedAt);

        // Persist the user turn *before* the LLM call. A crash or a
        // mid-stream error must not leave the user wondering whether
        // their input made it.
        await _repository.AddMessageAsync(userMessage, cancellationToken).ConfigureAwait(false);

        // First user turn ⇒ derive the title; bump updated_at either way.
        if (history.Count == 0)
        {
            session.SetTitleFromFirstMessage(request.Content, capturedAt);
        }
        else
        {
            session.Touch(capturedAt);
        }

        await _repository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        var stream = StreamReplyAsync(session, history, userMessage, cancellationToken);
        return Result<IAsyncEnumerable<LlmStreamChunk>>.Success(stream);
    }

    private async IAsyncEnumerable<LlmStreamChunk> StreamReplyAsync(
        ChatSession session,
        IReadOnlyList<ChatMessage> historyBeforeUser,
        ChatMessage userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var llmRequest = BuildLlmRequest(historyBeforeUser, userMessage);

        var stopwatch = Stopwatch.StartNew();
        var assistantBuffer = new System.Text.StringBuilder();
        string? finalModel = null;
        int? finalPromptTokens = null;
        int? finalCompletionTokens = null;
        TimeSpan finalLatency = TimeSpan.Zero;
        var sawError = false;

        await foreach (
            var chunk in _llmProvider
                .CompleteStreamAsync(llmRequest, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            switch (chunk.Kind)
            {
                case LlmStreamChunkKind.Delta:
                    if (!string.IsNullOrEmpty(chunk.Delta))
                    {
                        assistantBuffer.Append(chunk.Delta);
                    }

                    yield return chunk;
                    break;

                case LlmStreamChunkKind.Done:
                    finalModel = chunk.Model;
                    finalPromptTokens = chunk.PromptTokens;
                    finalCompletionTokens = chunk.CompletionTokens;
                    finalLatency =
                        chunk.Latency == TimeSpan.Zero ? stopwatch.Elapsed : chunk.Latency;
                    yield return chunk;
                    break;

                case LlmStreamChunkKind.Error:
                    sawError = true;
                    yield return chunk;
                    break;

                default:
                    // Unknown kind: forward unchanged. New kinds added by
                    // future ADRs surface to the client without the
                    // AppService dropping data.
                    yield return chunk;
                    break;
            }
        }

        stopwatch.Stop();

        // Persist the assistant turn only on a clean Done. Mid-stream
        // errors and zero-delta streams are skipped — the user turn is
        // already persisted, so the next request will resend the same
        // history without a phantom assistant entry.
        if (sawError || finalModel is null || assistantBuffer.Length == 0)
        {
            yield break;
        }

        var assistantMessage = ChatMessage.CreateAssistant(
            sessionId: session.Id,
            content: assistantBuffer.ToString(),
            model: finalModel,
            promptTokens: finalPromptTokens,
            completionTokens: finalCompletionTokens,
            createdAtUtc: _clock.UtcNow
        );

        await _repository
            .AddMessageAsync(assistantMessage, cancellationToken)
            .ConfigureAwait(false);

        session.Touch(_clock.UtcNow);
        await _repository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }

    private static LlmRequest BuildLlmRequest(
        IReadOnlyList<ChatMessage> historyBeforeUser,
        ChatMessage userMessage
    )
    {
        var messages = new List<LlmMessage>(historyBeforeUser.Count + 2)
        {
            new(LlmRole.System, SystemPrompt),
        };

        foreach (var msg in historyBeforeUser)
        {
            messages.Add(new LlmMessage(MapRole(msg.Role), msg.Content));
        }

        messages.Add(new LlmMessage(LlmRole.User, userMessage.Content));

        return new LlmRequest(
            Messages: messages,
            Model: null,
            Temperature: Temperature,
            MaxTokens: MaxTokens
        );
    }

    private static LlmRole MapRole(ChatRole role) =>
        role switch
        {
            ChatRole.User => LlmRole.User,
            ChatRole.Assistant => LlmRole.Assistant,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown ChatRole."),
        };

    private static ChatSessionDto ToDto(ChatSession session) =>
        new(
            Id: session.Id,
            Title: session.Title,
            CreatedAt: session.CreatedAt,
            UpdatedAt: session.UpdatedAt
        );

    private static ChatMessageDto ToDto(ChatMessage message) =>
        new(
            Id: message.Id,
            SessionId: message.SessionId,
            Role: message.Role == ChatRole.User ? "user" : "assistant",
            Content: message.Content,
            CreatedAt: message.CreatedAt,
            Model: message.Model,
            PromptTokens: message.PromptTokens,
            CompletionTokens: message.CompletionTokens
        );
}
