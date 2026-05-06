namespace Dawning.AgentOS.Application.Chat;

/// <summary>
/// Read-side projection of a <see cref="Domain.Chat.ChatMessage"/>.
/// </summary>
/// <param name="Id">UUIDv7 identifier of the message.</param>
/// <param name="SessionId">Identifier of the owning session.</param>
/// <param name="Role">Whether the turn was authored by the user or the assistant. Encoded as a lowercase string on the wire (<c>"user"</c> / <c>"assistant"</c>).</param>
/// <param name="Content">The literal text content of the turn.</param>
/// <param name="CreatedAt">UTC instant the turn was persisted.</param>
/// <param name="Model">Model identifier echoed by the provider for assistant turns; <c>null</c> for user turns.</param>
/// <param name="PromptTokens">Prompt-token count when reported; <c>null</c> for user turns or when the upstream did not report it.</param>
/// <param name="CompletionTokens">Completion-token count when reported; <c>null</c> for user turns or when the upstream did not report it.</param>
public sealed record ChatMessageDto(
    Guid Id,
    Guid SessionId,
    string Role,
    string Content,
    DateTimeOffset CreatedAt,
    string? Model,
    int? PromptTokens,
    int? CompletionTokens
);
