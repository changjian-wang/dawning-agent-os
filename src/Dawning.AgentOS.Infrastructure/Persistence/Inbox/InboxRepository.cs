using System.Globalization;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Inbox;
using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Inbox;

/// <summary>
/// Infrastructure implementation of <see cref="IInboxRepository"/> using
/// <c>Dawning.ORM.Dapper</c>. Per ADR-036 the repository follows the
/// same style as the dawning gateway stack: PO persistence entity +
/// attribute mapping + aggregate rehydrate on read.
/// </summary>
/// <remarks>
/// <para>
/// Each operation opens a fresh ADO.NET connection through
/// <see cref="IDbConnectionFactory"/> and disposes it via
/// <c>await using</c>; the factory itself is scoped, the repository
/// scoped accordingly.
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

    private readonly IDbConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task AddAsync(InboxItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = ToEntity(item);
        _ = await connection.InsertAsync(entity).ConfigureAwait(false);
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

        var entities = await connection
            .Builder<InboxItemEntity>()
            .OrderByDescending(x => x.CapturedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync()
            .ConfigureAwait(false);

        var items = new List<InboxItem>(entities.Count);
        foreach (var entity in entities)
        {
            items.Add(MapEntity(entity));
        }
        return items;
    }

    /// <inheritdoc />
    public async Task<InboxItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        // Use the QueryBuilder predicate path rather than connection.GetAsync<T>(id):
        // GetAsync routes the row through a dynamic-typed callsite which the runtime
        // binder fails to convert back to T (see Dawning.ORM.Dapper 1.3.0). The builder
        // path is statically typed end-to-end and works with the same attribute mapping.
        var idValue = id.ToString();
        var entity = await connection
            .Builder<InboxItemEntity>()
            .Where(x => x.Id == idValue)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return entity is null ? null : MapEntity(entity);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        return await connection
            .Builder<InboxItemEntity>()
            .CountAsync()
            .ConfigureAwait(false);
    }

    private static InboxItemEntity ToEntity(InboxItem item) =>
        new()
        {
            Id = item.Id.ToString(),
            Content = item.Content,
            Source = item.Source,
            CapturedAtUtc = item.CapturedAtUtc.ToString(
                IsoRoundTripFormat,
                CultureInfo.InvariantCulture
            ),
            CreatedAtUtc = item.CreatedAt.ToString(IsoRoundTripFormat, CultureInfo.InvariantCulture),
        };

    private static InboxItem MapEntity(InboxItemEntity entity)
    {
        var id = Guid.Parse(entity.Id, CultureInfo.InvariantCulture);
        var capturedAt = DateTimeOffset.Parse(
            entity.CapturedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
        var createdAt = DateTimeOffset.Parse(
            entity.CreatedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );

        return InboxItem.Rehydrate(id, entity.Content, entity.Source, capturedAt, createdAt);
    }
}
