using Microsoft.Extensions.Hosting;

namespace Dawning.AgentOS.Infrastructure.Persistence.Sqlite;

public sealed class SqliteSchemaHostedService(ISqliteSchemaBootstrapper schemaBootstrapper)
  : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    return schemaBootstrapper.InitializeAsync(cancellationToken);
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
}
