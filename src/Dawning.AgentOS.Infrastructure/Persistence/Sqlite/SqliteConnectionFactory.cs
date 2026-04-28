using Dawning.AgentOS.Application.Abstractions.System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.Persistence.Sqlite;

public sealed class SqliteConnectionFactory(
  IUserDataPathProvider userDataPathProvider,
  IOptions<AgentOsStorageOptions> options
) : ISqliteConnectionFactory
{
  public string DatabasePath =>
    Path.Combine(userDataPathProvider.GetUserDataDirectory(), options.Value.DatabaseFileName);

  public async ValueTask<SqliteConnection> OpenConnectionAsync(
    CancellationToken cancellationToken = default
  )
  {
    Directory.CreateDirectory(userDataPathProvider.GetUserDataDirectory());
    var connection = new SqliteConnection($"Data Source={DatabasePath}");
    await connection.OpenAsync(cancellationToken);
    return connection;
  }
}
