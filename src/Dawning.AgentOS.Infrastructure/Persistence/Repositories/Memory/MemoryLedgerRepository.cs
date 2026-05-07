using System.Globalization;
using Dawning.AgentOS.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Memory;
using Dawning.AgentOS.Infrastructure.Persistence.Entities.Memory;
using Dawning.ORM.Dapper;
using static Dawning.ORM.Dapper.SqlMapperExtensions;

namespace Dawning.AgentOS.Infrastructure.Persistence.Repositories.Memory;

/// <summary>
/// Infrastructure implementation of <see cref="IMemoryLedgerRepository"/>
/// using <c>Dawning.ORM.Dapper</c>. Per ADR-036 this repository follows
/// the same style as <see cref="Inbox.InboxRepository"/> and
/// <see cref="Chat.ChatSessionRepository"/>: PO persistence entity +
/// attribute mapping + aggregate rehydrate on read.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-033 §决策 L1 each operation opens a fresh ADO.NET connection
/// through <see cref="IDbConnectionFactory"/> and disposes it via
/// <c>await using</c>; the factory is scoped, the repository scoped
/// accordingly.
/// </para>
/// <para>
/// All timestamps round-trip as ISO-8601 strings through the <c>"O"</c>
/// format specifier — same convention as the other V0 repositories.
/// <see cref="MemoryLedgerEntry"/> rehydration uses
/// <see cref="MemoryLedgerEntry.Rehydrate"/> so no domain events fire on
/// load. Per ADR-033 §决策 G1 the soft-delete row is preserved
/// indefinitely; the default list / count paths exclude it via the
/// <see cref="MemoryStatus.SoftDeleted"/> filter.
/// </para>
/// <para>
/// Per ADR-033 §决策 I1 the update path overwrites only mutable columns
/// (<c>content</c>, <c>scope</c>, <c>sensitivity</c>, <c>status</c>,
/// <c>updated_at_utc</c>, <c>deleted_at_utc</c>). Immutable columns
/// (<c>source</c>, <c>is_explicit</c>, <c>confidence</c>,
/// <c>created_at_utc</c>) carry <see cref="IgnoreUpdateAttribute"/> on
/// <see cref="MemoryEntryEntity"/> so the generated UPDATE skips them.
/// </para>
/// </remarks>
public sealed class MemoryLedgerRepository(IDbConnectionFactory connectionFactory)
    : IMemoryLedgerRepository
{
    private const string IsoRoundTripFormat = "O";

    private readonly IDbConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task AddAsync(MemoryLedgerEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = ToEntity(entry);
        _ = await connection.InsertAsync(entity).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryLedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        // Use the QueryBuilder predicate path rather than connection.GetAsync<T>(id):
        // GetAsync routes the row through a dynamic-typed callsite which the runtime
        // binder fails to convert back to T (see Dawning.ORM.Dapper 1.3.0). The
        // builder path is statically typed end-to-end and is mandated by the
        // persistence-repository-conventions rule regardless of the upstream fix
        // in 1.3.1.
        var idValue = id.ToString();
        var entity = await connection
            .Builder<MemoryEntryEntity>()
            .Where(x => x.Id == idValue)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return entity is null ? null : MapEntity(entity);
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

        // Source / IsExplicit / Confidence / CreatedAtUtc carry [IgnoreUpdate]
        // on the entity so the generated UPDATE skips them — matching the prior
        // hand-written SQL that deliberately omitted those immutable columns
        // per ADR-033 §决策 I1.
        var entity = ToEntity(entry);
        return await connection.UpdateAsync(entity).ConfigureAwait(false);
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

        var entities = await ApplyFilter(
                connection.Builder<MemoryEntryEntity>(),
                statusFilter,
                includeSoftDeleted
            )
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync()
            .ConfigureAwait(false);

        var items = new List<MemoryLedgerEntry>(entities.Count);
        foreach (var entity in entities)
        {
            items.Add(MapEntity(entity));
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
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        return await ApplyFilter(
                connection.Builder<MemoryEntryEntity>(),
                statusFilter,
                includeSoftDeleted
            )
            .CountAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Composes the WHERE chain shared by <see cref="ListAsync"/> and
    /// <see cref="CountAsync"/>. The two filter modes are mutually
    /// exclusive — a non-null <paramref name="statusFilter"/> wins; only
    /// when no explicit filter is given does <paramref name="includeSoftDeleted"/>
    /// gate the soft-delete exclusion.
    /// </summary>
    private static QueryBuilder<MemoryEntryEntity> ApplyFilter(
        QueryBuilder<MemoryEntryEntity> builder,
        MemoryStatus? statusFilter,
        bool includeSoftDeleted
    )
    {
        if (statusFilter is { } status)
        {
            var statusValue = (int)status;
            return builder.Where(x => x.Status == statusValue);
        }

        if (!includeSoftDeleted)
        {
            const int softDeleted = (int)MemoryStatus.SoftDeleted;
            return builder.Where(x => x.Status != softDeleted);
        }

        return builder;
    }

    private static MemoryEntryEntity ToEntity(MemoryLedgerEntry entry) =>
        new()
        {
            Id = entry.Id.ToString(),
            Content = entry.Content,
            Scope = entry.Scope,
            Source = (int)entry.Source,
            IsExplicit = entry.IsExplicit ? 1 : 0,
            Confidence = entry.Confidence,
            Sensitivity = (int)entry.Sensitivity,
            Status = (int)entry.Status,
            CreatedAtUtc = FormatTimestamp(entry.CreatedAt),
            UpdatedAtUtc = FormatTimestamp(entry.UpdatedAtUtc),
            DeletedAtUtc = entry.DeletedAtUtc is { } d ? FormatTimestamp(d) : null,
        };

    private static MemoryLedgerEntry MapEntity(MemoryEntryEntity entity)
    {
        var id = Guid.Parse(entity.Id, CultureInfo.InvariantCulture);
        var createdAt = ParseTimestamp(entity.CreatedAtUtc);
        var updatedAt = ParseTimestamp(entity.UpdatedAtUtc);
        var deletedAt = entity.DeletedAtUtc is null
            ? (DateTimeOffset?)null
            : ParseTimestamp(entity.DeletedAtUtc);

        return MemoryLedgerEntry.Rehydrate(
            id: id,
            content: entity.Content,
            scope: entity.Scope,
            source: (MemorySource)entity.Source,
            isExplicit: entity.IsExplicit != 0,
            confidence: entity.Confidence,
            sensitivity: (MemorySensitivity)entity.Sensitivity,
            status: (MemoryStatus)entity.Status,
            createdAtUtc: createdAt,
            updatedAtUtc: updatedAt,
            deletedAtUtc: deletedAt
        );
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString(IsoRoundTripFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
}
