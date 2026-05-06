namespace Dawning.AgentOS.Domain.Chat;

/// <summary>
/// Repository port for <see cref="ChatSession"/> aggregates and their
/// child <see cref="ChatMessage"/> entities. Per ADR-022 the contract
/// lives in Domain so use cases and tests can depend on the interface
/// alone; the SQLite + Dapper adapter lives in
/// <c>Dawning.AgentOS.Infrastructure.Persistence.Chat</c>.
/// </summary>
/// <remarks>
/// Per ADR-032 §决策 D2 the V0 surface deliberately exposes only what
/// the four chat endpoints need: load a single session, list recent
/// sessions, load all messages for one session in chronological order,
/// add a new session / message, and update the session's
/// <c>title</c> + <c>updated_at</c> after activity.
/// </remarks>
public interface IChatSessionRepository
{
    /// <summary>
    /// Returns the session with the supplied id, or <c>null</c> when no
    /// such session exists.
    /// </summary>
    Task<ChatSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a paged window of sessions ordered by <c>updated_at DESC</c>.
    /// Bounds checking on <paramref name="limit"/> and
    /// <paramref name="offset"/> is the AppService's responsibility.
    /// </summary>
    Task<IReadOnlyList<ChatSession>> ListAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns all messages for <paramref name="sessionId"/> in
    /// <c>created_at ASC</c> order — the natural send order. Returns an
    /// empty list when the session has no messages or does not exist.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> LoadMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken
    );

    /// <summary>Persists a brand-new session.</summary>
    Task AddAsync(ChatSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Persists <see cref="ChatSession.Title"/> and
    /// <see cref="ChatSession.UpdatedAt"/> for an existing session.
    /// </summary>
    Task UpdateAsync(ChatSession session, CancellationToken cancellationToken);

    /// <summary>Persists a single message belonging to an existing session.</summary>
    Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken);
}
