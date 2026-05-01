using Dawning.AgentOS.Application.Abstractions;
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

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRuntimeStartTimeProvider, ProcessStartRuntimeStartTimeProvider>();

        return services;
    }
}
