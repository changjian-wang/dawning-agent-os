namespace Dawning.AgentOS.Application.Memory;

/// <summary>
/// Input for <c>PATCH /api/memory/{id}</c> per ADR-033 §决策 J1.
/// All fields are optional; <c>null</c> means "do not change". The
/// AppService rejects an entirely empty payload with
/// <c>memory.update.empty</c> (HTTP 400) rather than silently no-op.
/// </summary>
/// <remarks>
/// Per ADR-033 §决策 I1 source / created_at / is_explicit / confidence
/// are not mutable on the aggregate, so they are not exposed here.
/// Status transitions are enforced by the aggregate's state machine
/// and surface as <c>memory.invalidStatusTransition</c> (HTTP 422)
/// when the requested transition is not allowed from the current
/// status; the AppService translates the
/// <c>MemoryLedgerInvalidStatusTransitionException</c> into the error
/// code.
/// </remarks>
/// <param name="Content">New content; <c>null</c> = leave unchanged.</param>
/// <param name="Scope">New scope; <c>null</c> = leave unchanged.</param>
/// <param name="Sensitivity">New sensitivity tier; <c>null</c> = leave unchanged.</param>
/// <param name="Status">
/// New status; <c>null</c> = leave unchanged. Only legal transitions
/// from the aggregate's state machine are allowed; illegal transitions
/// (e.g. SoftDeleted → Archived) yield HTTP 422.
/// </param>
public sealed record UpdateMemoryEntryRequest(
    string? Content,
    string? Scope,
    Domain.Memory.MemorySensitivity? Sensitivity,
    Domain.Memory.MemoryStatus? Status
);
