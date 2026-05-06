namespace Dawning.AgentOS.Application.Chat;

/// <summary>
/// Wire shape returned by <c>POST /api/chat/sessions</c>. Per ADR-032
/// §决策 G1 the renderer is allowed to start sending messages
/// immediately after this response lands, so the server returns the
/// full session snapshot rather than just an id.
/// </summary>
/// <param name="Session">The newly created session (still has placeholder title).</param>
public sealed record CreateChatSessionResponse(ChatSessionDto Session);
