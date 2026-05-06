using Dawning.AgentOS.Application.Chat;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Application service facade for the chat use case. Per ADR-032
/// §决策 G1 V0 exposes four operations: create a session, list
/// sessions, list a session's messages, and stream the assistant's
/// reply to a user turn.
/// </summary>
public interface IChatAppService
{
    /// <summary>
    /// Creates a new empty chat session and returns its snapshot. Per
    /// ADR-032 §决策 I1 the renderer calls this on first user input
    /// before <see cref="SendMessageStreamAsync"/>.
    /// </summary>
    Task<Result<ChatSessionDto>> CreateSessionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns a page of chat sessions ordered by <c>updated_at DESC</c>.
    /// </summary>
    Task<Result<ChatSessionListPage>> ListSessionsAsync(
        ChatSessionListQuery query,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns all messages for <paramref name="sessionId"/> in send
    /// order. Per ADR-032 §决策 K1 a missing session yields a
    /// <c>chat.sessionNotFound</c> error mapped to HTTP 404.
    /// </summary>
    Task<Result<IReadOnlyList<ChatMessageDto>>> ListMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Persists <paramref name="request"/> as the user's next turn,
    /// streams the assistant's reply chunk by chunk, and persists the
    /// completed assistant turn after the stream's terminal
    /// <see cref="LlmStreamChunkKind.Done"/> chunk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per ADR-032 §决策 J1 the AppService assembles the per-call
    /// message list as: [built-in system prompt] + [persisted history
    /// in send order] + [the new user turn]. The system prompt is a
    /// constant in the implementation; clients cannot override it.
    /// </para>
    /// <para>
    /// Per ADR-032 §决策 K1 a missing session yields one
    /// <see cref="LlmStreamChunkKind.Error"/> chunk carrying
    /// <c>chat.sessionNotFound</c>; field-level validation failures
    /// (empty content, oversize content) return a
    /// <see cref="Result.Failure(DomainError[])"/> outcome before any
    /// streaming begins so the API layer can surface them as HTTP 400.
    /// </para>
    /// <para>
    /// Cancellation surfaces as <see cref="OperationCanceledException"/>
    /// propagated from the underlying <see cref="ILlmProvider"/>.
    /// </para>
    /// </remarks>
    Task<Result<IAsyncEnumerable<LlmStreamChunk>>> SendMessageStreamAsync(
        Guid sessionId,
        SendMessageRequest request,
        CancellationToken cancellationToken
    );
}
