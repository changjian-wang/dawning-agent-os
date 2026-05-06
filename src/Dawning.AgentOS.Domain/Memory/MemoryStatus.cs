namespace Dawning.AgentOS.Domain.Memory;

/// <summary>
/// Lifecycle state of a <see cref="MemoryLedgerEntry"/>.
/// Per ADR-033 §决策 G1 the V0 state machine has four states:
/// <see cref="Active"/> → <see cref="Corrected"/> → <see cref="Archived"/>
/// → <see cref="SoftDeleted"/>, plus a <see cref="SoftDeleted"/> →
/// <see cref="Active"/> restore path.
/// </summary>
/// <remarks>
/// Ordinal values are assigned explicitly so the persisted INTEGER
/// representation in <c>memory_entries.status</c> never silently shifts.
/// Legal transitions are enforced inside <see cref="MemoryLedgerEntry"/>;
/// the enum itself is just a value list.
/// </remarks>
public enum MemoryStatus
{
    /// <summary>
    /// Default state; entry is current and visible in the Memory pane.
    /// </summary>
    Active = 1,

    /// <summary>
    /// User has explicitly marked the entry as having been corrected.
    /// Still visible; still mutable.
    /// </summary>
    Corrected = 2,

    /// <summary>
    /// User chose to keep but no longer use this entry. Hidden from
    /// the default Memory pane list but recoverable.
    /// </summary>
    Archived = 3,

    /// <summary>
    /// User soft-deleted this entry. Per PURPOSE.md L3 红线 a 30-day
    /// recovery window is preserved (V0 keeps the row indefinitely;
    /// physical cleanup is parked for a future ADR).
    /// </summary>
    SoftDeleted = 4,
}
