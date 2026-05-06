namespace Dawning.AgentOS.Domain.Memory;

/// <summary>
/// Where a <see cref="MemoryLedgerEntry"/> originated from.
/// Per ADR-033 §决策 B1 V0 only persists <see cref="UserExplicit"/>;
/// the remaining values are reserved enum slots that future ADRs
/// will unlock without requiring a schema migration.
/// </summary>
/// <remarks>
/// Ordinal values are assigned explicitly so the persisted INTEGER
/// representation in <c>memory_entries.source</c> never silently
/// shifts when a value is inserted in the middle of the enum.
/// </remarks>
public enum MemorySource
{
    /// <summary>The user typed this entry directly into the Memory pane.</summary>
    UserExplicit = 1,

    /// <summary>
    /// Reserved (V0 unused): future "remember this" path triggered
    /// from a chat conversation.
    /// </summary>
    Conversation = 2,

    /// <summary>
    /// Reserved (V0 unused): future path that promotes an inbox-side
    /// action (summary / tags) into a long-term memory.
    /// </summary>
    InboxAction = 3,

    /// <summary>
    /// Reserved (V0 unused): future path for "user just corrected the
    /// agent" signals.
    /// </summary>
    Correction = 4,
}
