using System.Reflection;
using Dawning.AgentOS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.AgentOS.Application.DependencyInjection;

/// <summary>
/// Composition-root entry point for the Application layer. The API project
/// (or any other host) consumes the layer via a single
/// <see cref="AddApplication(IServiceCollection)"/> call; per ADR-023 §5
/// this places AppService registration knowledge inside the Application
/// project itself rather than making the API host scan a foreign assembly.
/// </summary>
/// <remarks>
/// <para>
/// The extension introduces the only NuGet dependency in the Application
/// project: <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>
/// (abstractions only, no concrete container). The architecture test
/// <c>Application_DoesNotReferenceFrameworkAdapterPackages</c> permits
/// this single package while keeping the concrete
/// <c>Microsoft.Extensions.DependencyInjection</c> container forbidden.
/// </para>
/// </remarks>
public static class ApplicationServiceCollectionExtensions
{
    private const string InterfacesNamespace = "Dawning.AgentOS.Application.Interfaces";
    private const string ServicesNamespace = "Dawning.AgentOS.Application.Services";
    private const string AppServiceSuffix = "AppService";

    /// <summary>
    /// Registers every <c>IXxxAppService</c> contract declared under
    /// <see cref="InterfacesNamespace"/> with its corresponding concrete
    /// implementation under <see cref="ServicesNamespace"/> as a scoped
    /// service.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The same collection to allow fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if an interface in <see cref="InterfacesNamespace"/> ends
    /// with <see cref="AppServiceSuffix"/> but no matching concrete class
    /// is found in <see cref="ServicesNamespace"/>, or if more than one
    /// candidate is found. Surfacing the failure at startup is preferred
    /// over a silent miss that only fires at request time.
    /// </exception>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(IRuntimeAppService).Assembly;
        var allTypes = assembly.GetTypes();

        var contracts = allTypes
            .Where(t =>
                t.IsInterface
                && t.Namespace == InterfacesNamespace
                && t.Name.StartsWith('I')
                && t.Name.EndsWith(AppServiceSuffix, StringComparison.Ordinal)
            )
            .ToArray();

        var implementations = allTypes
            .Where(t =>
                t.IsClass
                && !t.IsAbstract
                && t.Namespace == ServicesNamespace
                && t.Name.EndsWith(AppServiceSuffix, StringComparison.Ordinal)
            )
            .ToArray();

        foreach (var contract in contracts)
        {
            var matches = implementations.Where(impl => contract.IsAssignableFrom(impl)).ToArray();

            if (matches.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No implementation found in '{ServicesNamespace}' for AppService contract '{contract.FullName}'."
                );
            }

            if (matches.Length > 1)
            {
                var names = string.Join(", ", matches.Select(m => m.FullName));
                throw new InvalidOperationException(
                    $"Multiple implementations found in '{ServicesNamespace}' for AppService contract '{contract.FullName}': {names}. "
                        + "AppService auto-registration requires a single concrete implementation per contract."
                );
            }

            services.AddScoped(contract, matches[0]);
        }

        return services;
    }
}
