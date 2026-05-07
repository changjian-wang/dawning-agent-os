using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Entities.Inbox;

/// <summary>
/// ORM persistence shape for <c>inbox_items</c>. Kept separate from the
/// domain aggregate so the domain model stays behavior-first.
/// </summary>
[Table("inbox_items")]
internal sealed class InboxItemEntity
{
    [ExplicitKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("source")]
    public string? Source { get; set; }

    [Column("captured_at_utc")]
    public string CapturedAtUtc { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public string CreatedAtUtc { get; set; } = string.Empty;
}
