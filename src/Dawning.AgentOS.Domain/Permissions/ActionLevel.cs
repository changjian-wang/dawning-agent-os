namespace Dawning.AgentOS.Domain.Permissions;

/// <summary>
/// Risk classification for an agent action. Drives the default handling rule:
/// self-decide, require confirmation, or refuse.
/// </summary>
/// <remarks>
/// The four levels are a product invariant; new levels should not be added
/// without a deliberate product decision. Persisted values follow the numeric
/// ordinal, so existing entries must keep their values across versions.
/// </remarks>
public enum ActionLevel
{
    /// <summary>Read-only / informational. Query, search, summarize, classify, propose. Agent self-decides.</summary>
    L0 = 0,

    /// <summary>Reversible curation. Tag, file, archive, build index. Agent self-decides; reports after.</summary>
    L1 = 1,

    /// <summary>Content modification. Edit content, batch rename / move, write local state. Requires confirmation; diff or rollback path required.</summary>
    L2 = 2,

    /// <summary>High-risk irreversible. Delete, send mail, payment, external speech, git push, calendar write, permission / key changes. Requires explicit one-shot confirmation.</summary>
    L3 = 3,
}
