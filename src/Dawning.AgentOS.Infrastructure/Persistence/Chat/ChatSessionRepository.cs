using System.Globalization;
using Dapper;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Chat;

namespace Dawning.AgentOS.Infrastructure.Persistence.Chat;

/// <summary>
/// V0 Dapper-based implementation of <see cref="IChatSessionRepository"/>.
/// Per ADR-032 §决策 D2 each operation opens a fresh ADO.NET connection
/// through <see cref="IDbConnectionFactory"/> and disposes it via
/// <c>await using</c>; the factory itself is scoped, the repository
/// scoped accordingly.
/// </summary>
/// <remarks>
/// All timestamps round-trip as ISO-8601 strings through the <c>"O"</c>
/// format specifier — same convention as
/// <see cref="Inbox.InboxRepository"/>. Aggregates are rehydrated via
/// <see cref="ChatSession.Rehydrate"/> /
/// <see cref="ChatMessage.Rehydrate"/> so no domain events fire on load.
/// </remarks>
public sealed class ChatSessionRepository(IDbConnectionFactory connectionFactory)
    : IChatSessionRepository
{
    private const string IsoRoundTripFormat = "O";

    private const string InsertSessionSql =
        "INSERT INTO chat_sessions (id, title, created_at_utc, updated_at_utc) "
        + "VALUES (@id, @title, @createdAtUtc, @updatedAtUtc)";

    private const string UpdateSessionSql =
        "UPDATE chat_sessions SET title = @title, updated_at_utc = @updatedAtUtc "
        + "WHERE id = @id";

    private const string GetSessionSql =
        "SELECT id, title, created_at_utc, updated_at_utc "
        + "FROM chat_sessions WHERE id = @id LIMIT 1";

    private const string ListSessionsSql =
        "SELECT id, title, created_at_utc, updated_at_utc "
        + "FROM chat_sessions "
        + "ORDER BY updated_at_utc DESC, id DESC "
        + "LIMIT @limit OFFSET @offset";

    private const string InsertMessageSql =
        "INSERT INTO chat_messages "
        + "(id, session_id, role, content, created_at_utc, model, prompt_tokens, completion_tokens) "
        + "VALUES (@id, @sessionId, @role, @content, @createdAtUtc, @model, @promptTokens, @completionTokens)";

    private const string LoadMessagesSql =
        "SELECT id, session_id, role, content, created_at_utc, model, prompt_tokens, completion_tokens "
        + "FROM chat_messages WHERE session_id = @sessionId "
        + "ORDER BY created_at_utc ASC, id ASC";

    private readonly IDbConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<ChatSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection
            .QuerySingleOrDefaultAsync<ChatSessionRow>(
                new CommandDefinition(
                    GetSessionSql,
                    new { id = sessionId.ToString() },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        return row is null ? null : MapSession(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatSession>> ListAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection
            .QueryAsync<ChatSessionRow>(
                new CommandDefinition(
                    ListSessionsSql,
                    new { limit, offset },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        var sessions = new List<ChatSession>();
        foreach (var row in rows)
        {
            sessions.Add(MapSession(row));
        }
        return sessions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> LoadMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection
            .QueryAsync<ChatMessageRow>(
                new CommandDefinition(
                    LoadMessagesSql,
                    new { sessionId = sessionId.ToString() },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        var messages = new List<ChatMessage>();
        foreach (var row in rows)
        {
            messages.Add(MapMessage(row));
        }
        return messages;
    }

    /// <inheritdoc />
    public async Task AddAsync(ChatSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = new
        {
            id = session.Id.ToString(),
            title = session.Title,
            createdAtUtc = session.CreatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
            updatedAtUtc = session.UpdatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
        };

        await connection
            .ExecuteAsync(
                new CommandDefinition(
                    InsertSessionSql,
                    parameters,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ChatSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = new
        {
            id = session.Id.ToString(),
            title = session.Title,
            updatedAtUtc = session.UpdatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
        };

        await connection
            .ExecuteAsync(
                new CommandDefinition(
                    UpdateSessionSql,
                    parameters,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = new
        {
            id = message.Id.ToString(),
            sessionId = message.SessionId.ToString(),
            role = (int)message.Role,
            content = message.Content,
            createdAtUtc = message.CreatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
            model = message.Model,
            promptTokens = message.PromptTokens,
            completionTokens = message.CompletionTokens,
        };

        await connection
            .ExecuteAsync(
                new CommandDefinition(
                    InsertMessageSql,
                    parameters,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);
    }

    private static ChatSession MapSession(ChatSessionRow row) =>
        ChatSession.Rehydrate(
            id: Guid.Parse(row.Id, CultureInfo.InvariantCulture),
            title: row.Title,
            createdAt: ParseUtc(row.CreatedAtUtc),
            updatedAt: ParseUtc(row.UpdatedAtUtc)
        );

    private static ChatMessage MapMessage(ChatMessageRow row) =>
        ChatMessage.Rehydrate(
            id: Guid.Parse(row.Id, CultureInfo.InvariantCulture),
            sessionId: Guid.Parse(row.SessionId, CultureInfo.InvariantCulture),
            role: (ChatRole)row.Role,
            content: row.Content,
            createdAt: ParseUtc(row.CreatedAtUtc),
            model: row.Model,
            promptTokens: row.PromptTokens,
            completionTokens: row.CompletionTokens
        );

    private static DateTimeOffset ParseUtc(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );

    /// <summary>
    /// Dapper materialization shape for <c>chat_sessions</c>. Column
    /// names are snake_case per migration; the <c>matchNamesWithUnderscores</c>
    /// option enabled in the DI extensions handles the mapping.
    /// </summary>
    private sealed class ChatSessionRow
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dapper materialization shape for <c>chat_messages</c>.
    /// </summary>
    private sealed class ChatMessageRow
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public int Role { get; set; }
        public string Content { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string? Model { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
    }
}
