namespace Dawning.AgentOS.Domain.Memory;

/// <summary>
/// Repository port for the <see cref="MemoryLedgerEntry"/> aggregate.
/// Per ADR-033 §决策 I1 the V0 surface is intentionally minimal:
/// <see cref="AddAsync"/>, <see cref="GetByIdAsync"/>,
/// <see cref="UpdateAsync"/>, <see cref="ListAsync"/>, and
/// <see cref="CountAsync"/>. State transitions happen on the
/// aggregate (via <see cref="MemoryLedgerEntry"/> business methods);
/// <see cref="UpdateAsync"/> just persists the new shape, so we do
/// not expose a separate <c>SetStatusAsync</c> overload.
/// </summary>
/// <remarks>
/// <para>
/// Per DDD-canonical placement (ADR-026 §2 / ADR-032 §决策) the port
/// lives in the Domain layer and talks exclusively in domain types.
/// The Dapper / SQLite adapter lives under
/// <c>Dawning.AgentOS.Infrastructure.Persistence.Memory</c>.
/// </para>
/// <para>
/// Per ADR-033 §决策 J1 the read paths support filtering by
/// <see cref="MemoryStatus"/>; <c>null</c> means "all statuses
/// except <see cref="MemoryStatus.SoftDeleted"/>" — that exclusion
/// rule is carried by the application layer's default for the
/// list endpoint, while the repository itself faithfully passes
/// the filter through to SQL.
/// </para>
/// </remarks>
public interface IMemoryLedgerRepository
{
    /// <summary>
    /// Persists a newly created aggregate. Implementations must not
    /// dispatch domain events; the AppService owns event lifecycle.
    /// </summary>
    Task AddAsync(MemoryLedgerEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches a single aggregate by UUIDv7 identifier; returns
    /// <c>null</c> when no row matches. The application layer
    /// surfaces absence as <c>memory.notFound</c> (HTTP 404) per
    /// ADR-033 §决策 J1.
    /// </summary>
    Task<MemoryLedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the latest mutable state of an existing aggregate
    /// (content, scope, sensitivity, status, deletedAtUtc, updatedAtUtc).
    /// Per ADR-033 §决策 I1 source / created_at / is_explicit /
    /// confidence are immutable on the aggregate so this method
    /// never overwrites them.
    /// </summary>
    /// <returns>
    /// <c>true</c> when a row was updated; <c>false</c> when no row
    /// matches the aggregate id (caller surfaces this as not-found).
    /// </returns>
    Task<bool> UpdateAsync(MemoryLedgerEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a page of entries ordered by
    /// <c>updated_at_utc DESC, id DESC</c> per ADR-033 §决策 L1.
    /// </summary>
    /// <param name="statusFilter">
    /// When non-null, restrict to rows with the given status.
    /// When <c>null</c>, the application layer's default excludes
    /// <see cref="MemoryStatus.SoftDeleted"/>.
    /// </param>
    /// <param name="includeSoftDeleted">
    /// When <c>true</c> AND <paramref name="statusFilter"/> is
    /// <c>null</c>, soft-deleted entries are returned alongside
    /// the rest. Ignored when <paramref name="statusFilter"/>
    /// is non-null (a specific filter wins).
    /// </param>
    /// <param name="limit">Max rows to return (1..200, validated upstream).</param>
    /// <param name="offset">Rows to skip (≥ 0, validated upstream).</param>
    Task<IReadOnlyList<MemoryLedgerEntry>> ListAsync(
        MemoryStatus? statusFilter,
        bool includeSoftDeleted,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Total row count consistent with the same filters as
    /// <see cref="ListAsync"/>. Used for the list response's
    /// <c>total</c> field.
    /// </summary>
    Task<long> CountAsync(
        MemoryStatus? statusFilter,
        bool includeSoftDeleted,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Per ADR-038 §决策 A1 returns up to <paramref name="limit"/>
    /// <see cref="MemoryStatus.Active"/> entries whose
    /// <see cref="MemoryLedgerEntry.Content"/> contains any of the
    /// supplied <paramref name="keywords"/> (case-insensitive substring
    /// match — naive keyword retrieval, no embeddings, no FTS).
    /// </summary>
    /// <param name="keywords">
    /// Pre-tokenized keywords (each is a substring to test against
    /// <see cref="MemoryLedgerEntry.Content"/>). When the list is
    /// empty implementations must return an empty result without
    /// touching the database — an empty <c>OR</c>-chain would otherwise
    /// degrade to "match everything".
    /// </param>
    /// <param name="limit">
    /// Per ADR-038 §决策 C1 the application layer caps this at 5.
    /// Implementations may over-fetch internally to allow hit-count
    /// re-ranking but must respect this final return cap.
    /// </param>
    /// <param name="cancellationToken">Standard cancellation token.</param>
    /// <returns>
    /// Per ADR-038 §决策 A1 the rows are ordered by
    /// hit-count DESC, then <c>updated_at_utc DESC</c>, then
    /// <c>id DESC</c>. Hit-count ties (e.g. when only one keyword is
    /// supplied) collapse the ordering to the secondary keys.
    /// </returns>
    /// <remarks>
    /// Per ADR-038 §决策 E1 only <see cref="MemoryStatus.Active"/>
    /// rows are eligible; <c>Corrected / Archived / SoftDeleted</c>
    /// rows are filtered out by the implementation, not by the caller,
    /// so future status additions can be opted-in centrally without
    /// touching <see cref="ChatMemoryRetriever"/>-style consumers.
    /// </remarks>
    Task<IReadOnlyList<MemoryLedgerEntry>> SearchByKeywordsAsync(
        IReadOnlyList<string> keywords,
        int limit,
        CancellationToken cancellationToken
    );
}
