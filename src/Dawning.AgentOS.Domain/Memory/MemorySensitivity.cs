namespace Dawning.AgentOS.Domain.Memory;

/// <summary>
/// Sensitivity tier of a <see cref="MemoryLedgerEntry"/>.
/// Per ADR-033 §决策 F1 the ledger uses three explicit tiers so
/// "high-sensitivity" entries can never be silently auto-promoted
/// into a stable user profile (the rule encoded by ADR-007 §决策).
/// </summary>
/// <remarks>
/// Ordinal values are assigned explicitly so the persisted INTEGER
/// representation in <c>memory_entries.sensitivity</c> never silently
/// shifts.
/// </remarks>
public enum MemorySensitivity
{
    /// <summary>
    /// Default tier; safe to display in the Memory pane and to
    /// surface to LLMs in future recall pathways.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Sensitive; user has flagged this entry as not for casual
    /// recall. Future recall paths must apply additional gating.
    /// </summary>
    Sensitive = 2,

    /// <summary>
    /// High-sensitivity (health, finance, intimate relationships,
    /// identity, emotional state); per ADR-007 §决策 must never be
    /// auto-promoted from inferred to stable user profile.
    /// </summary>
    HighSensitive = 3,
}
