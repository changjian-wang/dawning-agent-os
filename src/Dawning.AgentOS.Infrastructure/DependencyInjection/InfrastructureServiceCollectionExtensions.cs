using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Application.Abstractions.System;
using Dawning.AgentOS.Domain.Repositories;
using Dawning.AgentOS.Infrastructure.Persistence.Repositories;
using Dawning.AgentOS.Infrastructure.Persistence.Sqlite;
using Dawning.AgentOS.Infrastructure.Security;
using Dawning.AgentOS.Infrastructure.System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration
  )
  {
    services
      .AddOptions<AgentOsStorageOptions>()
      .Bind(configuration.GetSection(AgentOsStorageOptions.SectionName));
    services.AddSingleton<
      IValidateOptions<AgentOsStorageOptions>,
      AgentOsStorageOptionsValidator
    >();

    services.AddSingleton<IClock, SystemClock>();
    services.AddSingleton<IUserDataPathProvider, UserDataPathProvider>();
    services.AddSingleton<IStartupTokenProvider, StartupTokenProvider>();
    services.AddSingleton<IStartupTokenValidator, StartupTokenValidator>();
    services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
    services.AddSingleton<ISqliteSchemaBootstrapper, SqliteSchemaBootstrapper>();
    services.AddHostedService<SqliteSchemaHostedService>();
    services.AddScoped<IApplicationUnitOfWork, PassthroughApplicationUnitOfWork>();
    services.AddScoped<IRuntimeCheckpointRepository, SqliteRuntimeCheckpointRepository>();

    return services;
  }
}
