namespace Dawning.AgentOS.Application.Chat;

/// <summary>
/// Read-side projection of a <see cref="Domain.Chat.ChatSession"/>.
/// Per ADR-032 §决策 D1 the V0 surface returns id + title + created /
/// updated timestamps; messages are loaded separately via
/// <c>GET /api/chat/sessions/{id}/messages</c>.
/// </summary>
/// <param name="Id">UUIDv7 identifier.</param>
/// <param name="Title">Display title; equals the placeholder until the first user turn lands.</param>
/// <param name="CreatedAt">UTC instant the session was created.</param>
/// <param name="UpdatedAt">UTC instant of the most recent activity.</param>
public sealed record ChatSessionDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
