namespace Dawning.AgentOS.Domain.Permissions;

/// <summary>
/// Stable, machine-readable identifier for an agent action.
/// </summary>
/// <remarks>
/// The vocabulary is open: new actions are added by introducing new static
/// instances or by constructing one with a fresh code. Codes use a dotted
/// segment form <c>"&lt;namespace&gt;.&lt;verb&gt;"</c> (lowercase, ASCII letters
/// / digits / dots only) to keep grouping and persistence stable.
/// Equality is value-based on <see cref="Code"/>.
/// </remarks>
public sealed record ActionKind
{
    /// <summary>Stable code, e.g. <c>"memory.write"</c>.</summary>
    public string Code { get; }

    /// <summary>Creates an action kind. The code must be non-empty.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is null, empty or whitespace.</exception>
    public ActionKind(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Action code must be non-empty.", nameof(code));
        }

        Code = code;
    }

    // ---------------------------------------------------------------- V0 vocabulary
    // Keep this list short and aligned with the MVP read-side curation slice.
    // New actions land here as static fields; do not scatter `new ActionKind("...")`
    // through call sites.

    /// <summary>L0 — summarize an explicitly provided piece of content.</summary>
    public static readonly ActionKind ReadSummarize = new("read.summarize");

    /// <summary>L0 — propose a category for an explicitly provided piece of content.</summary>
    public static readonly ActionKind ReadClassify = new("read.classify");

    /// <summary>L1 — apply tags to an item already inside the inbox.</summary>
    public static readonly ActionKind ReadTag = new("read.tag");

    /// <summary>L1 — add a user-provided item to the agent inbox.</summary>
    public static readonly ActionKind InboxAdd = new("inbox.add");

    /// <summary>L2 — write a new entry to the explicit Memory Ledger.</summary>
    public static readonly ActionKind MemoryWrite = new("memory.write");

    /// <summary>L3 — permanently delete an entry from the Memory Ledger.</summary>
    public static readonly ActionKind MemoryDelete = new("memory.delete");

    /// <inheritdoc />
    public override string ToString() => Code;
}
