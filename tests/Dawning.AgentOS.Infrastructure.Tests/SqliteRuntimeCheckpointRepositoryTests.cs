using Dawning.AgentOS.Application.DependencyInjection;
using Dawning.AgentOS.Domain.Repositories;
using Dawning.AgentOS.Domain.Runtime;
using Dawning.AgentOS.Infrastructure.DependencyInjection;
using Dawning.AgentOS.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.AgentOS.Infrastructure.Tests;

public sealed class SqliteRuntimeCheckpointRepositoryTests : IDisposable
{
  private readonly string _dataDirectory = Path.Combine(
    Path.GetTempPath(),
    "DawningAgentOS.Tests",
    Guid.NewGuid().ToString("N")
  );

  [Fact]
  public async Task Repository_round_trips_crud_and_basic_paging_through_sqlite_dapper()
  {
    await using var provider = BuildProvider();
    await using var scope = provider.CreateAsyncScope();
    var repository = scope.ServiceProvider.GetRequiredService<IRuntimeCheckpointRepository>();
    var connectionFactory = provider.GetRequiredService<ISqliteConnectionFactory>();
    var checkpoint = RuntimeCheckpoint.Create("storage-probe", "created", DateTimeOffset.UtcNow);

    await repository.AddAsync(checkpoint);
    var loaded = await repository.GetAsync(checkpoint.Id);

    Assert.NotNull(loaded);
    Assert.Equal("created", loaded.Value);
    Assert.True(File.Exists(connectionFactory.DatabasePath));

    checkpoint.UpdateValue("updated", DateTimeOffset.UtcNow.AddMinutes(1));
    await repository.UpdateAsync(checkpoint);
    var updated = await repository.GetAsync(checkpoint.Id);

    Assert.NotNull(updated);
    Assert.Equal("updated", updated.Value);

    var page = await repository.ListAsync(1, 10);

    Assert.Contains(page, item => item.Id == checkpoint.Id);

    await repository.DeleteAsync(checkpoint.Id);
    var deleted = await repository.GetAsync(checkpoint.Id);

    Assert.Null(deleted);
  }

  public void Dispose()
  {
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(_dataDirectory))
    {
      Directory.Delete(_dataDirectory, recursive: true);
    }
  }

  private ServiceProvider BuildProvider()
  {
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(
        new Dictionary<string, string?>
        {
          ["Storage:DataDirectory"] = _dataDirectory,
          ["StartupToken:Token"] = "test-token",
        }
      )
      .Build();
    var services = new ServiceCollection();
    services.AddApplication();
    services.AddDomainServices();
    services.AddInfrastructure(configuration);
    return services.BuildServiceProvider(validateScopes: true);
  }
}
