namespace Dawning.AgentOS.Application.Chat;

/// <summary>
/// Pagination input for <c>GET /api/chat/sessions</c>. Fields are
/// nullable because the endpoint accepts optional query parameters and
/// applies defaults from <c>ChatEndpoints</c>.
/// </summary>
/// <param name="Limit">Maximum number of sessions to return per page; must be in [1, 200].</param>
/// <param name="Offset">Skip count for pagination; must be ≥ 0.</param>
public sealed record ChatSessionListQuery(int Limit, int Offset);
