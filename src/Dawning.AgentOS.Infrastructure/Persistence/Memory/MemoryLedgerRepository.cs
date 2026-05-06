using System.Globalization;
using Dapper;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Memory;

namespace Dawning.AgentOS.Infrastructure.Persistence.Memory;

/// <summary>
/// V0 Dapper-based implementation of <see cref="IMemoryLedgerRepository"/>.
/// Per ADR-033 §决策 L1 each operation opens a fresh ADO.NET connection
/// through <see cref="IDbConnectionFactory"/> and disposes it via
/// <c>await using</c>; the factory is scoped, the repository scoped
/// accordingly.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-024 §F1 / §G1 Dapper appears strictly inside Infrastructure.
/// All timestamps round-trip as ISO-8601 strings through the
/// <c>"O"</c> format specifier — the same format used by
/// <see cref="Inbox.InboxRepository"/> and <c>ChatSessionRepository</c>.
/// </para>
/// <para>
/// <see cref="MemoryLedgerEntry"/> rehydration uses
/// <see cref="MemoryLedgerEntry.Rehydrate"/> so no domain events fire
/// on load. Per ADR-033 §决策 G1 the soft-delete row is preserved
/// indefinitely; the default list / count paths exclude it via
/// <c>status &lt;&gt; 4</c>.
/// </para>
/// </remarks>
public sealed class MemoryLedgerRepository(IDbConnectionFactory connectionFactory)
    : IMemoryLedgerRepository
{
    private const string IsoRoundTripFormat = "O";

    private const string SelectColumns =
        "id, content, scope, source, is_explicit, confidence, sensitivity, status, "
        + "created_at_utc, updated_at_utc, deleted_at_utc";

    private const string InsertSql =
        "INSERT INTO memory_entries "
        + "(id, content, scope, source, is_explicit, confidence, sensitivity, status, "
        + "created_at_utc, updated_at_utc, deleted_at_utc) "
        + "VALUES (@id, @content, @scope, @source, @isExplicit, @confidence, @sensitivity, @status, "
        + "@createdAtUtc, @updatedAtUtc, @deletedAtUtc)";

    private const string GetByIdSql =
        "SELECT " + SelectColumns + " FROM memory_entries WHERE id = @id LIMIT 1";

    // The list / count statements interpolate the WHERE clause from a
    // small set of constants, never from caller input — see
    // BuildFilterClause / OrderClause below.
    private const string OrderClause =
        " ORDER BY updated_at_utc DESC, id DESC LIMIT @limit OFFSET @offset";

    // Per ADR-033 §决策 I1 the update overwrites only mutable columns;
    // source / created_at_utc / is_explicit / confidence are immutable
    // on the aggregate so the SQL deliberately omits them.
    private const string UpdateSql =
        "UPDATE memory_entries SET "
        + "content = @content, "
        + "scope = @scope, "
        + "sensitivity = @sensitivity, "
        + "status = @status, "
        + "updated_at_utc = @updatedAtUtc, "
        + "deleted_at_utc = @deletedAtUtc "
        + "WHERE id = @id";

    private readonly IDbConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task AddAsync(MemoryLedgerEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = new
        {
            id = entry.Id.ToString(),
            content = entry.Content,
            scope = entry.Scope,
            source = (int)entry.Source,
            isExplicit = entry.IsExplicit ? 1 : 0,
            confidence = entry.Confidence,
            sensitivity = (int)entry.Sensitivity,
            status = (int)entry.Status,
            createdAtUtc = FormatTimestamp(entry.CreatedAt),
            updatedAtUtc = FormatTimestamp(entry.UpdatedAtUtc),
            deletedAtUtc = entry.DeletedAtUtc is { } d ? FormatTimestamp(d) : null,
        };

        await connection
            .ExecuteAsync(
                new CommandDefinition(InsertSql, parameters, cancellationToken: cancellationToken)
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryLedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection
            .QuerySingleOrDefaultAsync<MemoryEntryRow>(
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
    public async Task<bool> UpdateAsync(
        MemoryLedgerEntry entry,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = new
        {
            id = entry.Id.ToString(),
            content = entry.Content,
            scope = entry.Scope,
            sensitivity = (int)entry.Sensitivity,
            status = (int)entry.Status,
            updatedAtUtc = FormatTimestamp(entry.UpdatedAtUtc),
            deletedAtUtc = entry.DeletedAtUtc is { } d ? FormatTimestamp(d) : null,
        };

        var rows = await connection
            .ExecuteAsync(
                new CommandDefinition(UpdateSql, parameters, cancellationToken: cancellationToken)
            )
            .ConfigureAwait(false);

        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryLedgerEntry>> ListAsync(
        MemoryStatus? statusFilter,
        bool includeSoftDeleted,
        int limit,
        int offset,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var (whereClause, parameters) = BuildFilterClause(
            statusFilter,
            includeSoftDeleted,
            limit,
            offset
        );

        var sql = "SELECT " + SelectColumns + " FROM memory_entries" + whereClause + OrderClause;

        var rows = await connection
            .QueryAsync<MemoryEntryRow>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
            )
            .ConfigureAwait(false);

        var items = new List<MemoryLedgerEntry>();
        foreach (var row in rows)
        {
            items.Add(MapRow(row));
        }
        return items;
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(
        MemoryStatus? statusFilter,
        bool includeSoftDeleted,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var (whereClause, parameters) = BuildFilterClause(
            statusFilter,
            includeSoftDeleted,
            limit: null,
            offset: null
        );

        var sql = "SELECT COUNT(*) FROM memory_entries" + whereClause;

        return await connection
            .ExecuteScalarAsync<long>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the WHERE clause + bound parameters used by both
    /// <see cref="ListAsync"/> and <see cref="CountAsync"/>. The clause
    /// is composed from a small fixed set of fragments so caller input
    /// never reaches the SQL string verbatim — only the
    /// <c>@status</c> / <c>@limit</c> / <c>@offset</c> bound parameters
    /// carry user data.
    /// </summary>
    private static (string Clause, DynamicParameters Parameters) BuildFilterClause(
        MemoryStatus? statusFilter,
        bool includeSoftDeleted,
        int? limit,
        int? offset
    )
    {
        var parameters = new DynamicParameters();
        if (limit is { } l)
        {
            parameters.Add("limit", l);
        }
        if (offset is { } o)
        {
            parameters.Add("offset", o);
        }

        if (statusFilter is { } status)
        {
            parameters.Add("status", (int)status);
            return (" WHERE status = @status", parameters);
        }

        if (!includeSoftDeleted)
        {
            parameters.Add("excludedStatus", (int)MemoryStatus.SoftDeleted);
            return (" WHERE status <> @excludedStatus", parameters);
        }

        return (string.Empty, parameters);
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString(IsoRoundTripFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );

    private static MemoryLedgerEntry MapRow(MemoryEntryRow row)
    {
        var id = Guid.Parse(row.Id, CultureInfo.InvariantCulture);
        var createdAt = ParseTimestamp(row.CreatedAtUtc);
        var updatedAt = ParseTimestamp(row.UpdatedAtUtc);
        var deletedAt = row.DeletedAtUtc is null
            ? (DateTimeOffset?)null
            : ParseTimestamp(row.DeletedAtUtc);

        return MemoryLedgerEntry.Rehydrate(
            id: id,
            content: row.Content,
            scope: row.Scope,
            source: (MemorySource)row.Source,
            isExplicit: row.IsExplicit != 0,
            confidence: row.Confidence,
            sensitivity: (MemorySensitivity)row.Sensitivity,
            status: (MemoryStatus)row.Status,
            createdAtUtc: createdAt,
            updatedAtUtc: updatedAt,
            deletedAtUtc: deletedAt
        );
    }

    /// <summary>
    /// Dapper materialization shape; column names are snake_case per
    /// the migration, mapped through the <c>matchNamesWithUnderscores</c>
    /// option that <see cref="DependencyInjection.InfrastructureServiceCollectionExtensions"/>
    /// turns on at startup.
    /// </summary>
    private sealed class MemoryEntryRow
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public int Source { get; set; }
        public int IsExplicit { get; set; }
        public double Confidence { get; set; }
        public int Sensitivity { get; set; }
        public int Status { get; set; }
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public string? DeletedAtUtc { get; set; }
    }
}
