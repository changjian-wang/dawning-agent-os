using Dawning.AgentOS.Application.Memory;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Application service facade for the memory ledger use cases. Per
/// ADR-033 §决策 J1 V0 exposes five operations:
/// <list type="bullet">
///   <item><description><see cref="CreateExplicitAsync"/> — POST /api/memory</description></item>
///   <item><description><see cref="ListAsync"/> — GET /api/memory</description></item>
///   <item><description><see cref="GetByIdAsync"/> — GET /api/memory/{id}</description></item>
///   <item><description><see cref="UpdateAsync"/> — PATCH /api/memory/{id}</description></item>
///   <item><description><see cref="SoftDeleteAsync"/> — DELETE /api/memory/{id}</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Per ADR-033 §决策 B1 V0 only persists user-explicit writes; the
/// AppService forces <c>source = UserExplicit</c>, <c>isExplicit = true</c>,
/// <c>confidence = 1.0</c> regardless of what the request DTO
/// implies, and the <see cref="CreateMemoryEntryRequest"/> DTO does
/// not even expose those fields.
/// </remarks>
public interface IMemoryLedgerAppService
{
    /// <summary>
    /// Creates a new user-explicit memory entry.
    /// </summary>
    /// <returns>
    /// <see cref="Result{T}.Success"/> with the persisted DTO, or
    /// <see cref="Result{T}.Failure"/> with field-level errors when
    /// validation fails (HTTP 400).
    /// </returns>
    Task<Result<MemoryEntryDto>> CreateExplicitAsync(
        CreateMemoryEntryRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns a page of memory entries ordered by
    /// <c>updated_at_utc DESC, id DESC</c>. By default soft-deleted
    /// entries are excluded; pass <see cref="MemoryListQuery.IncludeSoftDeleted"/>
    /// = <c>true</c> (or a specific
    /// <see cref="Domain.Memory.MemoryStatus"/> filter) to see them.
    /// </summary>
    Task<Result<MemoryEntryListPage>> ListAsync(
        MemoryListQuery query,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Fetches a single entry by id. Returns
    /// <see cref="Result{T}.Failure"/> with
    /// <see cref="Memory.MemoryErrors.NotFoundCode"/> when no row
    /// matches (HTTP 404).
    /// </summary>
    Task<Result<MemoryEntryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a partial update. Returns
    /// <see cref="Memory.MemoryErrors.NotFoundCode"/> (HTTP 404) when
    /// the row is missing or
    /// <see cref="Memory.MemoryErrors.InvalidStatusTransitionCode"/>
    /// (HTTP 422) when the requested status transition is illegal.
    /// </summary>
    Task<Result<MemoryEntryDto>> UpdateAsync(
        Guid id,
        UpdateMemoryEntryRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Soft-deletes an entry; idempotent on already-soft-deleted rows
    /// is rejected via the state machine (HTTP 422). Returns the
    /// updated DTO so the renderer can re-render without a follow-up
    /// GET.
    /// </summary>
    Task<Result<MemoryEntryDto>> SoftDeleteAsync(Guid id, CancellationToken cancellationToken);
}
