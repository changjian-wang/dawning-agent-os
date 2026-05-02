namespace Dawning.AgentOS.Domain.Inbox;

/// <summary>
/// Repository port for the <see cref="InboxItem"/> aggregate. Per
/// ADR-026 §2 the port lives in the Domain layer (DDD-canonical placement)
/// and talks exclusively in domain types; concrete adapters live under
/// <c>Dawning.AgentOS.Infrastructure.Persistence.Inbox</c>.
/// </summary>
/// <remarks>
/// V0 only needs three operations:
/// <list type="bullet">
///   <item>
///     <description><see cref="AddAsync"/> — append a captured item.</description>
///   </item>
///   <item>
///     <description><see cref="ListAsync"/> — page over the store ordered by
///     <c>captured_at_utc DESC, id DESC</c> per ADR-026 §4.</description>
///   </item>
///   <item>
///     <description><see cref="CountAsync"/> — total row count for paging UI;
///     keeps Application off raw SQL per ADR-024 §K1.</description>
///   </item>
/// </list>
/// Update / delete are intentionally out of scope: ADR-026 §I2 rejected
/// full CRUD for V0 since no UX entry exists.
/// </remarks>
public interface IInboxRepository
{
    /// <summary>
    /// Persists a newly captured aggregate. Implementations must not
    /// dispatch domain events; the AppService owns event lifecycle.
    /// </summary>
    /// <param name="item">The aggregate to persist; must already have a non-empty id.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task AddAsync(InboxItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a page of items ordered most-recent-first per ADR-026 §4.
    /// </summary>
    /// <param name="limit">Max rows to return (1..200, validated upstream).</param>
    /// <param name="offset">Rows to skip (≥ 0, validated upstream).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>An immutable snapshot of aggregates, possibly empty.</returns>
    Task<IReadOnlyList<InboxItem>> ListAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Total row count, used for the list response's <c>total</c> field.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task<long> CountAsync(CancellationToken cancellationToken);
}
