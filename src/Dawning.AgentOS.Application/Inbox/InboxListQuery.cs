namespace Dawning.AgentOS.Application.Inbox;

/// <summary>
/// Pagination query for <see cref="Interfaces.IInboxAppService.ListAsync"/>.
/// Per ADR-026 §C2 V0 uses limit + offset; cursor pagination is deferred.
/// </summary>
/// <param name="Limit">Max rows to return; valid range [1, 200].</param>
/// <param name="Offset">Rows to skip; valid range [0, +∞).</param>
public sealed record InboxListQuery(int Limit, int Offset);
