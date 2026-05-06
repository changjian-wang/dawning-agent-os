namespace Dawning.AgentOS.Application.Inbox;

/// <summary>
/// Page response for <see cref="Interfaces.IInboxAppService.ListAsync"/>.
/// Per ADR-026 §7 the AppService surfaces <c>total</c> so the client can
/// derive <c>hasMore</c> via <c>offset + items.Count &lt; total</c>; no
/// dedicated <c>nextOffset</c> / <c>hasMore</c> fields are included.
/// </summary>
/// <param name="Items">The page of inbox items, ordered by capture instant DESC.</param>
/// <param name="Total">Total row count in the underlying store at query time.</param>
/// <param name="Limit">Echo of the requested limit, for client convenience.</param>
/// <param name="Offset">Echo of the requested offset, for client convenience.</param>
public sealed record InboxListPage(
    IReadOnlyList<InboxItemSnapshot> Items,
    long Total,
    int Limit,
    int Offset
);
