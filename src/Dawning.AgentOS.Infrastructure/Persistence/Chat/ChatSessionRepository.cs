using System.Globalization;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Chat;
using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Chat;

/// <summary>
/// Infrastructure implementation of <see cref="IChatSessionRepository"/>
/// using <c>Dawning.ORM.Dapper</c>. Per ADR-036 this repository follows
/// the same style as <see cref="Inbox.InboxRepository"/>: PO persistence
/// entity + attribute mapping + aggregate rehydrate on read.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-032 §决策 D2 each operation opens a fresh ADO.NET connection
/// through <see cref="IDbConnectionFactory"/> and disposes it via
/// <c>await using</c>; the factory itself is scoped, the repository
/// scoped accordingly.
/// </para>
/// <para>
/// All timestamps round-trip as ISO-8601 strings through the <c>"O"</c>
/// format specifier — same convention as <see cref="Inbox.InboxRepository"/>.
/// Aggregates rehydrate via <see cref="ChatSession.Rehydrate"/> /
/// <see cref="ChatMessage.Rehydrate"/> so no domain events fire on load.
/// </para>
/// </remarks>
public sealed class ChatSessionRepository(IDbConnectionFactory connectionFactory)
    : IChatSessionRepository
{
    private const string IsoRoundTripFormat = "O";

    private readonly IDbConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<ChatSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        // Use the QueryBuilder predicate path rather than connection.GetAsync<T>(id):
        // GetAsync routes the row through a dynamic-typed callsite which the runtime
        // binder fails to convert back to T (see Dawning.ORM.Dapper 1.3.0). The
        // builder path is statically typed end-to-end and works with the same
        // attribute mapping — and is mandated by the persistence-repository-conventions
        // rule regardless of the upstream fix in 1.3.1.
        var idValue = sessionId.ToString();
        var entity = await connection
            .Builder<ChatSessionEntity>()
            .Where(x => x.Id == idValue)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return entity is null ? null : MapSession(entity);
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

        var entities = await connection
            .Builder<ChatSessionEntity>()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync()
            .ConfigureAwait(false);

        var sessions = new List<ChatSession>(entities.Count);
        foreach (var entity in entities)
        {
            sessions.Add(MapSession(entity));
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

        var idValue = sessionId.ToString();
        var entities = await connection
            .Builder<ChatMessageEntity>()
            .Where(x => x.SessionId == idValue)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var messages = new List<ChatMessage>(entities.Count);
        foreach (var entity in entities)
        {
            messages.Add(MapMessage(entity));
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

        var entity = ToSessionEntity(session);
        _ = await connection.InsertAsync(entity).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ChatSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        // CreatedAtUtc carries [IgnoreUpdate] on the entity so the
        // generated UPDATE skips it — matching the prior hand-written
        // SQL which only set title and updated_at_utc.
        var entity = ToSessionEntity(session);
        _ = await connection.UpdateAsync(entity).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = ToMessageEntity(message);
        _ = await connection.InsertAsync(entity).ConfigureAwait(false);
    }

    private static ChatSessionEntity ToSessionEntity(ChatSession session) =>
        new()
        {
            Id = session.Id.ToString(),
            Title = session.Title,
            CreatedAtUtc = session.CreatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
            UpdatedAtUtc = session.UpdatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
        };

    private static ChatMessageEntity ToMessageEntity(ChatMessage message) =>
        new()
        {
            Id = message.Id.ToString(),
            SessionId = message.SessionId.ToString(),
            Role = (int)message.Role,
            Content = message.Content,
            CreatedAtUtc = message.CreatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
            Model = message.Model,
            PromptTokens = message.PromptTokens,
            CompletionTokens = message.CompletionTokens,
        };

    private static ChatSession MapSession(ChatSessionEntity entity) =>
        ChatSession.Rehydrate(
            id: Guid.Parse(entity.Id, CultureInfo.InvariantCulture),
            title: entity.Title,
            createdAt: ParseUtc(entity.CreatedAtUtc),
            updatedAt: ParseUtc(entity.UpdatedAtUtc)
        );

    private static ChatMessage MapMessage(ChatMessageEntity entity) =>
        ChatMessage.Rehydrate(
            id: Guid.Parse(entity.Id, CultureInfo.InvariantCulture),
            sessionId: Guid.Parse(entity.SessionId, CultureInfo.InvariantCulture),
            role: (ChatRole)entity.Role,
            content: entity.Content,
            createdAt: ParseUtc(entity.CreatedAtUtc),
            model: entity.Model,
            promptTokens: entity.PromptTokens,
            completionTokens: entity.CompletionTokens
        );

    private static DateTimeOffset ParseUtc(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
}
