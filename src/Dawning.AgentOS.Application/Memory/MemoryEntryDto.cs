namespace Dawning.AgentOS.Application.Memory;

/// <summary>
/// Read-side projection of a <see cref="Domain.Memory.MemoryLedgerEntry"/>
/// returned by GET / POST / PATCH endpoints. Per ADR-033 §决策 J1 the
/// ledger exposes all schema columns to the renderer so the Memory
/// pane can display source / confidence / is_explicit metadata
/// without an extra round trip — even though V0 only writes
/// <see cref="Domain.Memory.MemorySource.UserExplicit"/> entries with
/// <c>IsExplicit=true</c> and <c>Confidence=1.0</c>.
/// </summary>
/// <param name="Id">UUIDv7 identifier.</param>
/// <param name="Content">Memory content.</param>
/// <param name="Scope">Free-text scope.</param>
/// <param name="Source">Origin of the entry.</param>
/// <param name="IsExplicit">Whether the user explicitly authored the entry.</param>
/// <param name="Confidence">Confidence in [0.0, 1.0].</param>
/// <param name="Sensitivity">Sensitivity tier.</param>
/// <param name="Status">Lifecycle state.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent mutation.</param>
/// <param name="DeletedAt">UTC soft-delete timestamp; <c>null</c> while not soft-deleted.</param>
public sealed record MemoryEntryDto(
    Guid Id,
    string Content,
    string Scope,
    Domain.Memory.MemorySource Source,
    bool IsExplicit,
    double Confidence,
    Domain.Memory.MemorySensitivity Sensitivity,
    Domain.Memory.MemoryStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt
);
