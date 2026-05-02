using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Abstractions.Hosting;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Inbox;
using Dawning.AgentOS.Infrastructure.Hosting;
using Dawning.AgentOS.Infrastructure.Options;
using Dawning.AgentOS.Infrastructure.Persistence;
using Dawning.AgentOS.Infrastructure.Persistence.Inbox;
using Dawning.AgentOS.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.AgentOS.Infrastructure.DependencyInjection;

/// <summary>
/// Composition-root extension for the Infrastructure layer. Per ADR-023
/// §6 the API host chains <c>AddApplication() → AddInfrastructure() →
/// AddApi()</c>; this method registers concrete adapters for the ports
/// declared in <c>Application/Abstractions/</c>.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure-layer services on
    /// <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The same collection for fluent chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ADR-026 §5: Dapper materializes snake_case columns into
        // PascalCase row classes; this is a process-wide setting and
        // is idempotent across calls.
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Time ports (ADR-017 / ADR-022).
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRuntimeStartTimeProvider, ProcessStartRuntimeStartTimeProvider>();

        // Persistence ports (ADR-024 §5). The path provider is a
        // singleton because the per-platform path never changes during
        // a process lifetime; the connection factory and schema
        // initializer are scoped so that cancellation tokens flow
        // naturally with the request scope (and so the hosted-service
        // bootstrap can build its own scope explicitly).
        services.AddSingleton<IAppDataPathProvider, AppDataPathProvider>();
        services.AddOptions<SqliteOptions>();
        services.AddScoped<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddScoped<ISchemaInitializer, SqliteSchemaInitializer>();
        services.AddHostedService<SchemaInitializerHostedService>();

        // Aggregate repositories (ADR-026 §5). The repository is scoped
        // alongside the connection factory; per-call connection lifetime
        // (ADR-024 §E1) keeps the repository safely re-entrant.
        services.AddScoped<IInboxRepository, InboxRepository>();

        return services;
    }
}
