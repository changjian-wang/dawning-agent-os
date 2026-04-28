using Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Sqlite;

public sealed class SqliteSchemaBootstrapper(ISqliteConnectionFactory connectionFactory)
  : ISqliteSchemaBootstrapper
{
  private static readonly SemaphoreSlim InitializationLock = new(1, 1);
  private bool _initialized;

  public async Task InitializeAsync(CancellationToken cancellationToken = default)
  {
    if (_initialized)
    {
      return;
    }

    await InitializationLock.WaitAsync(cancellationToken);
    try
    {
      if (_initialized)
      {
        return;
      }

      await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
      var command = new CommandDefinition(
        """
        CREATE TABLE IF NOT EXISTS schema_versions (
          version INTEGER PRIMARY KEY,
          applied_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS runtime_checkpoints (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          value TEXT NOT NULL,
          timestamp INTEGER NOT NULL,
          created_at TEXT NOT NULL,
          updated_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_runtime_checkpoints_timestamp
          ON runtime_checkpoints(timestamp);

        INSERT OR IGNORE INTO schema_versions(version, applied_at)
        VALUES (1, @AppliedAt);
        """,
        new { AppliedAt = DateTimeOffset.UtcNow.ToString("O") },
        cancellationToken: cancellationToken
      );

      await connection.ExecuteAsync(command);
      _initialized = true;
    }
    finally
    {
      InitializationLock.Release();
    }
  }
}
