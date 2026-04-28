using Dapper;
using Dawning.AgentOS.Domain.Repositories;
using Dawning.AgentOS.Domain.Runtime;
using Dawning.AgentOS.Infrastructure.Persistence.Dapper;
using Dawning.AgentOS.Infrastructure.Persistence.Sqlite;
using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Repositories;

public sealed class SqliteRuntimeCheckpointRepository(
  ISqliteConnectionFactory connectionFactory,
  ISqliteSchemaBootstrapper schemaBootstrapper
) : IRuntimeCheckpointRepository
{
  public async Task AddAsync(
    RuntimeCheckpoint checkpoint,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentNullException.ThrowIfNull(checkpoint);

    await schemaBootstrapper.InitializeAsync(cancellationToken);
    await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();
    await connection.InsertAsync(ToRow(checkpoint));
  }

  public async Task<RuntimeCheckpoint?> GetAsync(
    Guid id,
    CancellationToken cancellationToken = default
  )
  {
    if (id == Guid.Empty)
    {
      throw new ArgumentException("Runtime checkpoint id cannot be empty.", nameof(id));
    }

    await schemaBootstrapper.InitializeAsync(cancellationToken);
    await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
    var command = new CommandDefinition(
      """
      SELECT id, name, value, timestamp, created_at AS CreatedAt, updated_at AS UpdatedAt
      FROM runtime_checkpoints
      WHERE id = @Id;
      """,
      new { Id = id.ToString("D") },
      cancellationToken: cancellationToken
    );
    var row = await connection.QuerySingleOrDefaultAsync<RuntimeCheckpointRow>(command);
    return row is null ? null : ToDomain(row);
  }

  public async Task<IReadOnlyList<RuntimeCheckpoint>> ListAsync(
    int page,
    int pageSize,
    CancellationToken cancellationToken = default
  )
  {
    var normalizedPage = page < 1 ? 1 : page;
    var normalizedPageSize = pageSize < 1 ? 20 : pageSize;
    var offset = (normalizedPage - 1) * normalizedPageSize;

    await schemaBootstrapper.InitializeAsync(cancellationToken);
    await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
    var command = new CommandDefinition(
      """
      SELECT id, name, value, timestamp, created_at AS CreatedAt, updated_at AS UpdatedAt
      FROM runtime_checkpoints
      ORDER BY timestamp DESC, name ASC
      LIMIT @PageSize OFFSET @Offset;
      """,
      new { PageSize = normalizedPageSize, Offset = offset },
      cancellationToken: cancellationToken
    );
    var rows = await connection.QueryAsync<RuntimeCheckpointRow>(command);
    return rows.Select(ToDomain).ToArray();
  }

  public async Task UpdateAsync(
    RuntimeCheckpoint checkpoint,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentNullException.ThrowIfNull(checkpoint);

    await schemaBootstrapper.InitializeAsync(cancellationToken);
    await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();
    await connection.UpdateAsync(ToRow(checkpoint));
  }

  public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
  {
    var checkpoint = await GetAsync(id, cancellationToken);
    if (checkpoint is null)
    {
      return;
    }

    await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();
    await connection.DeleteAsync(ToRow(checkpoint));
  }

  private static RuntimeCheckpointRow ToRow(RuntimeCheckpoint checkpoint)
  {
    return new RuntimeCheckpointRow
    {
      Id = checkpoint.Id.ToString("D"),
      Name = checkpoint.Name,
      Value = checkpoint.Value,
      Timestamp = checkpoint.Timestamp,
      CreatedAt = checkpoint.CreatedAt.ToString("O"),
      UpdatedAt = checkpoint.UpdatedAt.ToString("O"),
    };
  }

  private static RuntimeCheckpoint ToDomain(RuntimeCheckpointRow row)
  {
    return RuntimeCheckpoint.Rehydrate(
      Guid.Parse(row.Id),
      row.Name,
      row.Value,
      row.Timestamp,
      DateTimeOffset.Parse(row.CreatedAt),
      DateTimeOffset.Parse(row.UpdatedAt)
    );
  }
}
