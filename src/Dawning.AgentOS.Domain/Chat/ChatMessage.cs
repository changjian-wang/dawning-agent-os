using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Domain.Chat;

/// <summary>
/// A single turn in a chat conversation. Per ADR-032 §决策 D1 / E1
/// only user and assistant turns are persisted; the built-in system
/// prompt is prepended to each LLM request by the AppService and never
/// makes it to the <c>chat_messages</c> table.
/// </summary>
/// <remarks>
/// <para>
/// Each message is its own <see cref="Entity{TId}"/>; the parent
/// <see cref="ChatSession"/> is referenced by
/// <see cref="SessionId"/> rather than via a navigation collection,
/// keeping the aggregate boundary tight (no eager load of history).
/// </para>
/// <para>
/// <see cref="CreateUser(Guid, string, DateTimeOffset)"/> and
/// <see cref="CreateAssistant(Guid, string, string, int?, int?, DateTimeOffset)"/>
/// are the business factories. Per ADR-022 invariant violations throw;
/// loading from the repository goes through
/// <see cref="Rehydrate(Guid, Guid, ChatRole, string, DateTimeOffset, string?, int?, int?)"/>.
/// </para>
/// </remarks>
public sealed class ChatMessage : Entity<Guid>
{
    /// <summary>Per ADR-032 §决策 D1 max user-content length (UTF-16 code units).</summary>
    public const int MaxContentLength = 8_192;

    /// <summary>The session this turn belongs to.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Whether this turn was authored by the user or the assistant.</summary>
    public ChatRole Role { get; private set; }

    /// <summary>The literal text content of the turn.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Model identifier echoed by the LLM provider for assistant turns;
    /// always <c>null</c> for user turns.
    /// </summary>
    public string? Model { get; private set; }

    /// <summary>
    /// Prompt-token count reported by the LLM provider for assistant
    /// turns; <c>null</c> when the upstream did not report it or this
    /// is a user turn.
    /// </summary>
    public int? PromptTokens { get; private set; }

    /// <summary>
    /// Completion-token count reported by the LLM provider for assistant
    /// turns; <c>null</c> when the upstream did not report it or this
    /// is a user turn.
    /// </summary>
    public int? CompletionTokens { get; private set; }

    private ChatMessage(
        Guid id,
        Guid sessionId,
        ChatRole role,
        string content,
        DateTimeOffset createdAt,
        string? model,
        int? promptTokens,
        int? completionTokens
    )
        : base(id, createdAt)
    {
        SessionId = sessionId;
        Role = role;
        Content = content;
        Model = model;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
    }

    private ChatMessage() { }

    /// <summary>
    /// Creates a user-authored turn anchored at <paramref name="createdAtUtc"/>.
    /// </summary>
    /// <param name="sessionId">Owning session id; must not be empty.</param>
    /// <param name="content">User input; non-whitespace, length ≤ <see cref="MaxContentLength"/>.</param>
    /// <param name="createdAtUtc">UTC instant of capture (offset must be zero).</param>
    /// <returns>The new entity with a UUIDv7 id stamped at <paramref name="createdAtUtc"/>.</returns>
    /// <exception cref="ArgumentException">When invariants are violated.</exception>
    public static ChatMessage CreateUser(
        Guid sessionId,
        string content,
        DateTimeOffset createdAtUtc
    )
    {
        ValidateCommon(sessionId, content, createdAtUtc);

        return new ChatMessage(
            id: Guid.CreateVersion7(createdAtUtc),
            sessionId: sessionId,
            role: ChatRole.User,
            content: content,
            createdAt: createdAtUtc,
            model: null,
            promptTokens: null,
            completionTokens: null
        );
    }

    /// <summary>
    /// Creates an assistant-generated turn anchored at <paramref name="createdAtUtc"/>.
    /// </summary>
    /// <param name="sessionId">Owning session id; must not be empty.</param>
    /// <param name="content">Generated text; non-whitespace, length ≤ <see cref="MaxContentLength"/>.</param>
    /// <param name="model">Model identifier echoed by the provider; non-whitespace.</param>
    /// <param name="promptTokens">Optional prompt-token count.</param>
    /// <param name="completionTokens">Optional completion-token count.</param>
    /// <param name="createdAtUtc">UTC instant the assistant turn finished (offset must be zero).</param>
    /// <returns>The new entity with a UUIDv7 id stamped at <paramref name="createdAtUtc"/>.</returns>
    /// <exception cref="ArgumentException">When invariants are violated.</exception>
    public static ChatMessage CreateAssistant(
        Guid sessionId,
        string content,
        string model,
        int? promptTokens,
        int? completionTokens,
        DateTimeOffset createdAtUtc
    )
    {
        ValidateCommon(sessionId, content, createdAtUtc);

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                "Assistant model identifier must be non-empty.",
                nameof(model)
            );
        }

        if (promptTokens is < 0)
        {
            throw new ArgumentException(
                "promptTokens must be non-negative when reported.",
                nameof(promptTokens)
            );
        }

        if (completionTokens is < 0)
        {
            throw new ArgumentException(
                "completionTokens must be non-negative when reported.",
                nameof(completionTokens)
            );
        }

        return new ChatMessage(
            id: Guid.CreateVersion7(createdAtUtc),
            sessionId: sessionId,
            role: ChatRole.Assistant,
            content: content,
            createdAt: createdAtUtc,
            model: model,
            promptTokens: promptTokens,
            completionTokens: completionTokens
        );
    }

    /// <summary>
    /// Rehydrates a message from persisted row data without raising any
    /// domain events.
    /// </summary>
    public static ChatMessage Rehydrate(
        Guid id,
        Guid sessionId,
        ChatRole role,
        string content,
        DateTimeOffset createdAt,
        string? model,
        int? promptTokens,
        int? completionTokens
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Rehydrated id must not be Guid.Empty.", nameof(id));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Rehydrated sessionId must not be Guid.Empty.",
                nameof(sessionId)
            );
        }

        var msg = new ChatMessage
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
        };
        msg.Id = id;
        msg.CreatedAt = createdAt;
        return msg;
    }

    private static void ValidateCommon(Guid sessionId, string content, DateTimeOffset createdAtUtc)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("sessionId must not be Guid.Empty.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Chat message content must be non-empty.", nameof(content));
        }

        if (content.Length > MaxContentLength)
        {
            throw new ArgumentException(
                $"Chat message content length {content.Length} exceeds the {MaxContentLength}-character limit.",
                nameof(content)
            );
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "createdAtUtc must be a UTC instant (offset = TimeSpan.Zero).",
                nameof(createdAtUtc)
            );
        }
    }
}
