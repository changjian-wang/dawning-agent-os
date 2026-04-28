using Dawning.AgentOS.Application.Runtime;
using Dawning.AgentOS.Domain.Services.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.AgentOS.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddScoped<IRuntimeStatusService, RuntimeStatusService>();
    return services;
  }

  public static IServiceCollection AddDomainServices(this IServiceCollection services)
  {
    services.AddSingleton<IPermissionDecisionService, PermissionDecisionService>();
    return services;
  }
}
