namespace Dawning.AgentOS.Infrastructure.Persistence.Sqlite;

public interface ISqliteSchemaBootstrapper
{
  Task InitializeAsync(CancellationToken cancellationToken = default);
}
