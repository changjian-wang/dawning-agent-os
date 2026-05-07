using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Memory;

/// <summary>
/// ORM persistence shape for <c>memory_entries</c>. Kept separate from
/// <see cref="Domain.Memory.MemoryLedgerEntry"/> so the domain aggregate
/// stays behavior-first. Per ADR-036 the Infrastructure layer owns this PO.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Source"/>, <see cref="IsExplicit"/>, <see cref="Confidence"/>
/// and <see cref="CreatedAtUtc"/> are marked <see cref="IgnoreUpdateAttribute"/>
/// to mirror the immutability rules in ADR-033 §决策 I1: those columns are
/// only ever written on the initial insert. The original hand-written
/// UPDATE statement deliberately omitted them; preserving the same wire
/// shape keeps the behavior bit-for-bit identical under
/// <c>connection.UpdateAsync(entity)</c>.
/// </para>
/// <para>
/// <see cref="IsExplicit"/> is stored as <c>INTEGER</c> (0 / 1) rather than
/// <c>BOOLEAN</c> because SQLite has no boolean primitive — the schema
/// migration declares it as <c>INTEGER</c>.
/// </para>
/// </remarks>
[Table("memory_entries")]
internal sealed class MemoryEntryEntity
{
    [ExplicitKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("scope")]
    public string Scope { get; set; } = string.Empty;

    [IgnoreUpdate]
    [Column("source")]
    public int Source { get; set; }

    [IgnoreUpdate]
    [Column("is_explicit")]
    public int IsExplicit { get; set; }

    [IgnoreUpdate]
    [Column("confidence")]
    public double Confidence { get; set; }

    [Column("sensitivity")]
    public int Sensitivity { get; set; }

    [Column("status")]
    public int Status { get; set; }

    [IgnoreUpdate]
    [Column("created_at_utc")]
    public string CreatedAtUtc { get; set; } = string.Empty;

    [Column("updated_at_utc")]
    public string UpdatedAtUtc { get; set; } = string.Empty;

    [Column("deleted_at_utc")]
    public string? DeletedAtUtc { get; set; }
}
