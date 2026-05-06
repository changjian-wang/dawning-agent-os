using System.Collections.Immutable;

namespace Dawning.AgentOS.Application.Chat;

/// <summary>
/// Page of <see cref="ChatSessionDto"/> returned by
/// <c>GET /api/chat/sessions</c>. Mirrors <c>InboxListPage</c>'s shape
/// for consistency: items + the current limit / offset echoed back.
/// </summary>
/// <param name="Items">The page of sessions in <c>updated_at DESC</c> order.</param>
/// <param name="Limit">Echo of the request limit.</param>
/// <param name="Offset">Echo of the request offset.</param>
public sealed record ChatSessionListPage(
    ImmutableArray<ChatSessionDto> Items,
    int Limit,
    int Offset
);
