using Dawning.AgentOS.Api.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.AgentOS.Api.DependencyInjection;

/// <summary>
/// Composition-root extension for the API host. Per ADR-023 §6 the
/// <c>Program.cs</c> only chains <c>AddApplication() →
/// AddInfrastructure() → AddApi()</c>; this method registers everything
/// the API layer owns: option binding, exception handler, and middleware
/// pipeline configuration that is not order-sensitive.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the API-layer services on <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same collection for fluent chaining.</returns>
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ApiHostOptions>()
            .Bind(configuration.GetSection(ApiHostOptions.SectionName));

        services
            .AddOptions<StartupTokenOptions>()
            .Bind(configuration.GetSection(StartupTokenOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddProblemDetails();

        return services;
    }
}
