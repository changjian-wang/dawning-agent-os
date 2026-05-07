using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Chat;

/// <summary>
/// ORM persistence shape for <c>chat_sessions</c>. Kept separate from
/// <see cref="Domain.Chat.ChatSession"/> so the domain aggregate stays
/// behavior-first. Per ADR-036 the Infrastructure layer owns this PO.
/// </summary>
/// <remarks>
/// <see cref="CreatedAtUtc"/> is marked <see cref="IgnoreUpdateAttribute"/>
/// because the original hand-written UPDATE statement deliberately omitted
/// <c>created_at_utc</c> — only <c>title</c> and <c>updated_at_utc</c> are
/// mutable on the aggregate. Marking it preserves the same wire-level
/// semantics under <c>connection.UpdateAsync(entity)</c>.
/// </remarks>
[Table("chat_sessions")]
internal sealed class ChatSessionEntity
{
    [ExplicitKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [IgnoreUpdate]
    [Column("created_at_utc")]
    public string CreatedAtUtc { get; set; } = string.Empty;

    [Column("updated_at_utc")]
    public string UpdatedAtUtc { get; set; } = string.Empty;
}
