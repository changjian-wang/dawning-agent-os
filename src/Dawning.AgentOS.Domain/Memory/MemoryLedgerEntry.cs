using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Domain.Memory;

/// <summary>
/// Aggregate root for a single user-managed long-term memory record.
/// Per ADR-033 §决策 A1 the V0 schema is intentionally aligned with
/// <see cref="Inbox.InboxItem"/> and <c>ChatSession</c>: a UUIDv7
/// identifier, a <c>Create</c> business factory and a
/// <c>Rehydrate</c> persistence factory, plus a small state machine
/// that enforces legal transitions internally.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-033 §决策 B1 V0 only persists <see cref="MemorySource.UserExplicit"/>
/// entries; the remaining <see cref="MemorySource"/> values are reserved enum
/// slots that future ADRs will unlock without requiring a schema migration.
/// </para>
/// <para>
/// State-machine transitions raise
/// <see cref="MemoryLedgerInvalidStatusTransitionException"/> when called
/// from a status that does not allow them. The Application layer catches
/// the exception and surfaces it as <c>memory.invalidStatusTransition</c>
/// (HTTP 422). Domain code does not produce field-level errors directly;
/// validation lives one layer up.
/// </para>
/// <para>
/// V0 raises no domain events: the dispatcher closure ADR is still open
/// (ADR-022 §10) and inbox / chat already operate without dispatch.
/// The aggregate stays event-ready by extending
/// <see cref="AggregateRoot{TId}"/> so future event paths drop in cleanly.
/// </para>
/// </remarks>
public sealed class MemoryLedgerEntry : AggregateRoot<Guid>
{
    /// <summary>Per ADR-033 §决策 J1 max content length (UTF-16 code units).</summary>
    public const int MaxContentLength = 4_096;

    /// <summary>Per ADR-033 §决策 C1 max scope length (UTF-16 code units).</summary>
    public const int MaxScopeLength = 128;

    /// <summary>Per ADR-033 §决策 C1 default scope when caller omits it.</summary>
    public const string DefaultScope = "global";

    /// <summary>The human-readable memory content.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Free-text scope marker (e.g. <c>"global"</c>, <c>"project:dawning"</c>).
    /// Per ADR-033 §决策 C1 V0 does not enforce a controlled vocabulary;
    /// dogfood will narrow the values.
    /// </summary>
    public string Scope { get; private set; } = DefaultScope;

    /// <summary>
    /// Where the entry came from. Set at construction and never changed
    /// (rehydration aside): allowing later mutation would break the
    /// invariant that <see cref="MemorySource.UserExplicit"/> entries
    /// stem from a real user keystroke.
    /// </summary>
    public MemorySource Source { get; private set; }

    /// <summary>
    /// Whether this entry was explicitly written by the user
    /// (<c>true</c>) or inferred by the agent (<c>false</c>). V0 only
    /// supports <c>true</c>; the field is kept on schema for future
    /// inference paths.
    /// </summary>
    public bool IsExplicit { get; private set; }

    /// <summary>
    /// Confidence in the entry, 0.0–1.0. V0 user-explicit entries are
    /// always 1.0; the field is kept on schema for future inference
    /// paths that may persist a lower confidence.
    /// </summary>
    public double Confidence { get; private set; }

    /// <summary>Sensitivity tier; see <see cref="MemorySensitivity"/>.</summary>
    public MemorySensitivity Sensitivity { get; private set; }

    /// <summary>Lifecycle state; see <see cref="MemoryStatus"/>.</summary>
    public MemoryStatus Status { get; private set; }

    /// <summary>UTC timestamp of the most recent mutation.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// UTC timestamp of the soft-delete operation. <c>null</c> while
    /// <see cref="Status"/> ≠ <see cref="MemoryStatus.SoftDeleted"/>.
    /// </summary>
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    private MemoryLedgerEntry(
        Guid id,
        string content,
        string scope,
        MemorySource source,
        bool isExplicit,
        double confidence,
        MemorySensitivity sensitivity,
        DateTimeOffset createdAtUtc
    )
        : base(id, createdAtUtc)
    {
        Content = content;
        Scope = scope;
        Source = source;
        IsExplicit = isExplicit;
        Confidence = confidence;
        Sensitivity = sensitivity;
        Status = MemoryStatus.Active;
        UpdatedAtUtc = createdAtUtc;
        DeletedAtUtc = null;
    }

    private MemoryLedgerEntry() { }

    /// <summary>
    /// Creates a new ledger entry. V0 callers (the application layer)
    /// only invoke this with <see cref="MemorySource.UserExplicit"/> and
    /// <paramref name="isExplicit"/> = <c>true</c> per ADR-033 §决策 B1,
    /// but the domain method itself accepts any combination so future
    /// inference paths can land without re-opening the aggregate.
    /// </summary>
    /// <param name="content">Required, non-whitespace, length ≤ <see cref="MaxContentLength"/>.</param>
    /// <param name="scope">Optional override of <see cref="DefaultScope"/>; if non-null must be non-whitespace and ≤ <see cref="MaxScopeLength"/>.</param>
    /// <param name="source">Origin of the entry.</param>
    /// <param name="isExplicit">Whether the entry was explicitly authored.</param>
    /// <param name="confidence">Confidence in [0.0, 1.0].</param>
    /// <param name="sensitivity">Sensitivity tier.</param>
    /// <param name="createdAtUtc">UTC instant of creation; offset must be zero.</param>
    /// <returns>The newly created aggregate.</returns>
    /// <exception cref="ArgumentException">When invariants are violated.</exception>
    public static MemoryLedgerEntry Create(
        string content,
        string? scope,
        MemorySource source,
        bool isExplicit,
        double confidence,
        MemorySensitivity sensitivity,
        DateTimeOffset createdAtUtc
    )
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Memory content must be non-empty.", nameof(content));
        }

        if (content.Length > MaxContentLength)
        {
            throw new ArgumentException(
                $"Memory content length {content.Length} exceeds the {MaxContentLength}-character limit.",
                nameof(content)
            );
        }

        var resolvedScope = scope ?? DefaultScope;
        if (string.IsNullOrWhiteSpace(resolvedScope))
        {
            throw new ArgumentException(
                "Memory scope, when provided, must be non-whitespace.",
                nameof(scope)
            );
        }

        if (resolvedScope.Length > MaxScopeLength)
        {
            throw new ArgumentException(
                $"Memory scope length {resolvedScope.Length} exceeds the {MaxScopeLength}-character limit.",
                nameof(scope)
            );
        }

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentException(
                $"Memory source '{source}' is not a defined MemorySource value.",
                nameof(source)
            );
        }

        if (!Enum.IsDefined(sensitivity))
        {
            throw new ArgumentException(
                $"Memory sensitivity '{sensitivity}' is not a defined MemorySensitivity value.",
                nameof(sensitivity)
            );
        }

        if (double.IsNaN(confidence) || confidence < 0.0 || confidence > 1.0)
        {
            throw new ArgumentException(
                $"Memory confidence must be a finite value in [0.0, 1.0]; got {confidence}.",
                nameof(confidence)
            );
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "createdAtUtc must be a UTC instant (offset = TimeSpan.Zero).",
                nameof(createdAtUtc)
            );
        }

        var id = Guid.CreateVersion7(createdAtUtc);
        return new MemoryLedgerEntry(
            id,
            content,
            resolvedScope,
            source,
            isExplicit,
            confidence,
            sensitivity,
            createdAtUtc
        );
    }

    /// <summary>
    /// Rehydrates an aggregate from persisted row data. Per ADR-022
    /// this path must not raise domain events; loading is not a
    /// business action.
    /// </summary>
    public static MemoryLedgerEntry Rehydrate(
        Guid id,
        string content,
        string scope,
        MemorySource source,
        bool isExplicit,
        double confidence,
        MemorySensitivity sensitivity,
        MemoryStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? deletedAtUtc
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Rehydrated id must not be Guid.Empty.", nameof(id));
        }

        var entry = new MemoryLedgerEntry
        {
            Content = content,
            Scope = scope,
            Source = source,
            IsExplicit = isExplicit,
            Confidence = confidence,
            Sensitivity = sensitivity,
            Status = status,
            UpdatedAtUtc = updatedAtUtc,
            DeletedAtUtc = deletedAtUtc,
        };
        entry.Id = id;
        entry.CreatedAt = createdAtUtc;
        return entry;
    }

    // ===================================================================
    // State-machine transitions. Per ADR-033 §决策 A1 each method
    // enforces legal pre-statuses internally; illegal calls raise
    // MemoryLedgerInvalidStatusTransitionException. Per the same
    // section UpdateContent / UpdateScope / UpdateSensitivity / Mark
    // Corrected / Archive / SoftDelete / Restore mutate UpdatedAtUtc;
    // the caller supplies the timestamp so domain code stays clock-free.
    // ===================================================================

    /// <summary>Updates the entry's content. Allowed only in <see cref="MemoryStatus.Active"/> or <see cref="MemoryStatus.Corrected"/>.</summary>
    public void UpdateContent(string content, DateTimeOffset updatedAtUtc)
    {
        EnsureStatusIsMutable(nameof(UpdateContent));
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Memory content must be non-empty.", nameof(content));
        }
        if (content.Length > MaxContentLength)
        {
            throw new ArgumentException(
                $"Memory content length {content.Length} exceeds the {MaxContentLength}-character limit.",
                nameof(content)
            );
        }

        Content = content;
        StampUpdated(updatedAtUtc);
    }

    /// <summary>Updates the entry's scope. Allowed only in <see cref="MemoryStatus.Active"/> or <see cref="MemoryStatus.Corrected"/>.</summary>
    public void UpdateScope(string scope, DateTimeOffset updatedAtUtc)
    {
        EnsureStatusIsMutable(nameof(UpdateScope));
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Memory scope must be non-whitespace.", nameof(scope));
        }
        if (scope.Length > MaxScopeLength)
        {
            throw new ArgumentException(
                $"Memory scope length {scope.Length} exceeds the {MaxScopeLength}-character limit.",
                nameof(scope)
            );
        }

        Scope = scope;
        StampUpdated(updatedAtUtc);
    }

    /// <summary>Updates the entry's sensitivity. Allowed only in <see cref="MemoryStatus.Active"/> or <see cref="MemoryStatus.Corrected"/>.</summary>
    public void UpdateSensitivity(MemorySensitivity sensitivity, DateTimeOffset updatedAtUtc)
    {
        EnsureStatusIsMutable(nameof(UpdateSensitivity));
        if (!Enum.IsDefined(sensitivity))
        {
            throw new ArgumentException(
                $"Memory sensitivity '{sensitivity}' is not a defined MemorySensitivity value.",
                nameof(sensitivity)
            );
        }

        Sensitivity = sensitivity;
        StampUpdated(updatedAtUtc);
    }

    /// <summary>
    /// Marks the entry as having been corrected. Allowed only from
    /// <see cref="MemoryStatus.Active"/>. Idempotent calls (already
    /// <see cref="MemoryStatus.Corrected"/>) raise the transition
    /// exception so callers don't silently double-mark.
    /// </summary>
    public void MarkCorrected(DateTimeOffset updatedAtUtc)
    {
        if (Status != MemoryStatus.Active)
        {
            throw new MemoryLedgerInvalidStatusTransitionException(Status, nameof(MarkCorrected));
        }

        Status = MemoryStatus.Corrected;
        StampUpdated(updatedAtUtc);
    }

    /// <summary>
    /// Archives the entry. Allowed from <see cref="MemoryStatus.Active"/>
    /// or <see cref="MemoryStatus.Corrected"/>.
    /// </summary>
    public void Archive(DateTimeOffset updatedAtUtc)
    {
        if (Status != MemoryStatus.Active && Status != MemoryStatus.Corrected)
        {
            throw new MemoryLedgerInvalidStatusTransitionException(Status, nameof(Archive));
        }

        Status = MemoryStatus.Archived;
        StampUpdated(updatedAtUtc);
    }

    /// <summary>
    /// Soft-deletes the entry per ADR-033 §决策 G1 / PURPOSE.md L3
    /// 红线. Allowed from <see cref="MemoryStatus.Active"/>,
    /// <see cref="MemoryStatus.Corrected"/>, or
    /// <see cref="MemoryStatus.Archived"/>.
    /// </summary>
    public void SoftDelete(DateTimeOffset updatedAtUtc)
    {
        if (
            Status != MemoryStatus.Active
            && Status != MemoryStatus.Corrected
            && Status != MemoryStatus.Archived
        )
        {
            throw new MemoryLedgerInvalidStatusTransitionException(Status, nameof(SoftDelete));
        }

        Status = MemoryStatus.SoftDeleted;
        DeletedAtUtc = updatedAtUtc;
        StampUpdated(updatedAtUtc);
    }

    /// <summary>
    /// Restores a soft-deleted entry. Allowed only from
    /// <see cref="MemoryStatus.SoftDeleted"/>; the entry returns to
    /// <see cref="MemoryStatus.Active"/>.
    /// </summary>
    public void Restore(DateTimeOffset updatedAtUtc)
    {
        if (Status != MemoryStatus.SoftDeleted)
        {
            throw new MemoryLedgerInvalidStatusTransitionException(Status, nameof(Restore));
        }

        Status = MemoryStatus.Active;
        DeletedAtUtc = null;
        StampUpdated(updatedAtUtc);
    }

    private void EnsureStatusIsMutable(string action)
    {
        if (Status != MemoryStatus.Active && Status != MemoryStatus.Corrected)
        {
            throw new MemoryLedgerInvalidStatusTransitionException(Status, action);
        }
    }

    private void StampUpdated(DateTimeOffset updatedAtUtc)
    {
        if (updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "updatedAtUtc must be a UTC instant (offset = TimeSpan.Zero).",
                nameof(updatedAtUtc)
            );
        }

        if (updatedAtUtc < CreatedAt)
        {
            throw new ArgumentException(
                $"updatedAtUtc {updatedAtUtc:O} must be >= CreatedAt {CreatedAt:O}.",
                nameof(updatedAtUtc)
            );
        }

        UpdatedAtUtc = updatedAtUtc;
    }
}
