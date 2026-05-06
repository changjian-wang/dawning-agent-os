namespace Dawning.AgentOS.Application.Memory;

/// <summary>
/// Page-of-entries response returned by <c>GET /api/memory</c> per
/// ADR-033 §决策 J1.
/// </summary>
/// <param name="Items">Entries on this page.</param>
/// <param name="Total">Total matching the same filter as <paramref name="Items"/>.</param>
/// <param name="Limit">The limit echoed back from the query.</param>
/// <param name="Offset">The offset echoed back from the query.</param>
public sealed record MemoryEntryListPage(
    IReadOnlyList<MemoryEntryDto> Items,
    long Total,
    int Limit,
    int Offset
);
