using System.Data.Common;
using System.Globalization;
using Dapper;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Inbox;

namespace Dawning.AgentOS.Infrastructure.Persistence.Inbox;

/// <summary>
/// V0 Dapper-based implementation of <see cref="IInboxRepository"/>. Per
/// ADR-026 §5 each operation opens a fresh ADO.NET connection through
/// <see cref="IDbConnectionFactory"/> and disposes it via
/// <c>await using</c>; the factory itself is scoped, the repository
/// scoped accordingly.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-024 §F1 / §G1 this class is the first place Dapper appears in
/// the codebase; the LayeringTests architecture rules already forbid
/// Dapper from Application / Domain / Api, so the dependency stays
/// strictly contained inside Infrastructure.
/// </para>
/// <para>
/// All timestamps round-trip as ISO-8601 strings through the
/// <c>"O"</c> format specifier, the same format the schema initializer
/// uses for <c>__schema_version.applied_at</c>. <see cref="InboxItem"/>
/// rehydration uses <see cref="InboxItem.Rehydrate(Guid, string, string?, DateTimeOffset, DateTimeOffset)"/>
/// so no domain events fire on load.
/// </para>
/// </remarks>
public sealed class InboxRepository(IDbConnectionFactory connectionFactory) : IInboxRepository
{
    private const string IsoRoundTripFormat = "O";

    private const string InsertSql =
        "INSERT INTO inbox_items "
        + "(id, content, source, captured_at_utc, created_at_utc) "
        + "VALUES (@id, @content, @source, @capturedAtUtc, @createdAtUtc)";

    private const string ListSql =
        "SELECT id, content, source, captured_at_utc, created_at_utc "
        + "FROM inbox_items "
        + "ORDER BY captured_at_utc DESC, id DESC "
        + "LIMIT @limit OFFSET @offset";

    private const string GetByIdSql =
        "SELECT id, content, source, captured_at_utc, created_at_utc "
        + "FROM inbox_items "
        + "WHERE id = @id "
        + "LIMIT 1";

    private const string CountSql = "SELECT COUNT(*) FROM inbox_items";

    private readonly IDbConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task AddAsync(InboxItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = new
        {
            id = item.Id.ToString(),
            content = item.Content,
            source = item.Source,
            capturedAtUtc = item.CapturedAtUtc.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
            createdAtUtc = item.CreatedAt.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
        };

        await connection
            .ExecuteAsync(
                new CommandDefinition(InsertSql, parameters, cancellationToken: cancellationToken)
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InboxItem>> ListAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection
            .QueryAsync<InboxItemRow>(
                new CommandDefinition(
                    ListSql,
                    new { limit, offset },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        var items = new List<InboxItem>();
        foreach (var row in rows)
        {
            items.Add(MapRow(row));
        }
        return items;
    }

    /// <inheritdoc />
    public async Task<InboxItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection
            .QuerySingleOrDefaultAsync<InboxItemRow>(
                new CommandDefinition(
                    GetByIdSql,
                    new { id = id.ToString() },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        return row is null ? null : MapRow(row);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        return await connection
            .ExecuteScalarAsync<long>(
                new CommandDefinition(CountSql, cancellationToken: cancellationToken)
            )
            .ConfigureAwait(false);
    }

    private static InboxItem MapRow(InboxItemRow row)
    {
        var id = Guid.Parse(row.Id, CultureInfo.InvariantCulture);
        var capturedAt = DateTimeOffset.Parse(
            row.CapturedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
        var createdAt = DateTimeOffset.Parse(
            row.CreatedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );

        return InboxItem.Rehydrate(id, row.Content, row.Source, capturedAt, createdAt);
    }

    /// <summary>
    /// Dapper materialization shape; column names are snake_case per
    /// the migration, mapped through the <c>matchNamesWithUnderscores</c>
    /// option that <see cref="InfrastructureServiceCollectionExtensions"/>
    /// turns on at startup.
    /// </summary>
    private sealed class InboxItemRow
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string CapturedAtUtc { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
    }
}
