using Dawning.AgentOS.Application.Memory;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Cross-aggregate coordinator that promotes an inbox item into the
/// memory ledger. Per ADR-034 §决策 D1 V0 introduces a dedicated
/// AppService rather than extending <see cref="IMemoryLedgerAppService"/>
/// or the inbox AppServices: the existing memory facade enforces
/// <c>source = UserExplicit</c> per ADR-033 §决策 B1 and breaking that
/// rail would muddy the contract; the inbox AppServices conversely
/// must not know that memory exists per ADR-021 layer-edge rules.
/// </summary>
/// <remarks>
/// V0 stamps every promoted entry with
/// <see cref="Domain.Memory.MemorySource.InboxAction"/>,
/// <c>isExplicit = true</c>, <c>confidence = 1.0</c>,
/// <c>sensitivity = Normal</c>, and the fixed scope
/// <c>"inbox"</c> (ADR-034 §决策 A1 / B1 / C1). Repeated promotion of
/// the same inbox item produces multiple ledger rows by design (ADR-034
/// §决策 F1 — no dedup); the user can soft-delete duplicates from the
/// memory pane.
/// </remarks>
public interface IInboxToMemoryAppService
{
    /// <summary>
    /// Reads the inbox item identified by <paramref name="inboxItemId"/>
    /// and writes its content as a new memory ledger entry.
    /// </summary>
    /// <param name="inboxItemId">UUIDv7 of the inbox item to promote.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Success"/> with the persisted ledger
    /// projection, or <see cref="Result{T}.Failure"/> with
    /// <see cref="Inbox.InboxErrors.ItemNotFoundCode"/> when the inbox
    /// item is missing (HTTP 404 in the API layer).
    /// </returns>
    Task<Result<MemoryEntryDto>> PromoteAsync(
        Guid inboxItemId,
        CancellationToken cancellationToken
    );
}
