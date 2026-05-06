namespace Dawning.AgentOS.Application.Memory;

/// <summary>
/// Pagination and filtering input for <c>GET /api/memory</c> per
/// ADR-033 §决策 J1.
/// </summary>
/// <param name="Limit">Page size; bounds-checked by the AppService.</param>
/// <param name="Offset">Rows to skip (≥ 0); bounds-checked by the AppService.</param>
/// <param name="Status">
/// Optional status filter parsed (case-insensitive) into
/// <see cref="Domain.Memory.MemoryStatus"/>; legal values
/// <c>"Active"</c> / <c>"Corrected"</c> / <c>"Archived"</c> /
/// <c>"SoftDeleted"</c>. When <c>null</c> the application service
/// excludes <see cref="Domain.Memory.MemoryStatus.SoftDeleted"/> by
/// default unless <paramref name="IncludeSoftDeleted"/> is <c>true</c>.
/// Unknown values surface as <c>memory.status.invalid</c> (HTTP 400).
/// </param>
/// <param name="IncludeSoftDeleted">
/// When <c>true</c> AND <paramref name="Status"/> is <c>null</c>,
/// soft-deleted entries are included alongside the rest. Ignored
/// when <paramref name="Status"/> is non-null (a specific filter wins).
/// </param>
public sealed record MemoryListQuery(
    int Limit,
    int Offset,
    string? Status,
    bool IncludeSoftDeleted
);
