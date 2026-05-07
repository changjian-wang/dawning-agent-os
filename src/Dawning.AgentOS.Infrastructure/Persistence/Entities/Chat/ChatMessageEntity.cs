using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Entities.Chat;

/// <summary>
/// ORM persistence shape for <c>chat_messages</c>. Kept separate from
/// <see cref="Domain.Chat.ChatMessage"/> so the domain aggregate stays
/// behavior-first. Per ADR-036 the Infrastructure layer owns this PO.
/// </summary>
/// <remarks>
/// Chat messages are append-only — no UPDATE path exists today. The
/// columns are nonetheless marked plain (not <see cref="IgnoreUpdateAttribute"/>)
/// so a future audit-style edit, if added, defaults to the safe shape.
/// </remarks>
[Table("chat_messages")]
internal sealed class ChatMessageEntity
{
    [ExplicitKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Mirrors <see cref="Domain.Chat.ChatRole"/>'s ordinal value.</summary>
    [Column("role")]
    public int Role { get; set; }

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public string CreatedAtUtc { get; set; } = string.Empty;

    [Column("model")]
    public string? Model { get; set; }

    [Column("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [Column("completion_tokens")]
    public int? CompletionTokens { get; set; }
}
