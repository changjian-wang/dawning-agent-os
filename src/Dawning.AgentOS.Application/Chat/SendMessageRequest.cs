namespace Dawning.AgentOS.Application.Chat;

/// <summary>
/// Wire shape consumed by <c>POST /api/chat/sessions/{id}/messages</c>.
/// Per ADR-032 §决策 G1 the request carries only the user turn; the
/// session id is in the URL and the system prompt + history are
/// reconstructed server-side.
/// </summary>
/// <param name="Content">User input; non-whitespace, length ≤ <see cref="Domain.Chat.ChatMessage.MaxContentLength"/>.</param>
public sealed record SendMessageRequest(string Content);
