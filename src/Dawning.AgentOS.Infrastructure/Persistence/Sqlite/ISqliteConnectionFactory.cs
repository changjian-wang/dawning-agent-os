using Microsoft.Data.Sqlite;

namespace Dawning.AgentOS.Infrastructure.Persistence.Sqlite;

public interface ISqliteConnectionFactory
{
  string DatabasePath { get; }

  ValueTask<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
