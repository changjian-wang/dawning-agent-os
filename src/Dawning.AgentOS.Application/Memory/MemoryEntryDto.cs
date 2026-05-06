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
/// <remarks>
/// Per ADR-023 architectural rule the Application DTO surface stays
/// off the Dawning.AgentOS.Domain assembly so that Api can keep its
/// reference graph free of Domain. Enum-typed columns surface as
/// PascalCase strings (e.g. <c>"UserExplicit"</c>, <c>"Active"</c>);
/// <see cref="Services.MemoryLedgerAppService"/> stamps the values
/// from the aggregate's enum via <c>ToString()</c>.
/// </remarks>
/// <param name="Id">UUIDv7 identifier.</param>
/// <param name="Content">Memory content.</param>
/// <param name="Scope">Free-text scope.</param>
/// <param name="Source">Origin of the entry (PascalCase MemorySource name).</param>
/// <param name="IsExplicit">Whether the user explicitly authored the entry.</param>
/// <param name="Confidence">Confidence in [0.0, 1.0].</param>
/// <param name="Sensitivity">Sensitivity tier (PascalCase MemorySensitivity name).</param>
/// <param name="Status">Lifecycle state (PascalCase MemoryStatus name).</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent mutation.</param>
/// <param name="DeletedAt">UTC soft-delete timestamp; <c>null</c> while not soft-deleted.</param>
public sealed record MemoryEntryDto(
    Guid Id,
    string Content,
    string Scope,
    string Source,
    bool IsExplicit,
    double Confidence,
    string Sensitivity,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt
);
