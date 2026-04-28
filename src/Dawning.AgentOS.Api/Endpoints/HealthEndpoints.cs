using Dawning.AgentOS.Application.Runtime;

namespace Dawning.AgentOS.Api.Endpoints;

public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
  {
    endpoints
      .MapGet(
        "/health",
        (IRuntimeStatusService runtimeStatusService) => Results.Ok(runtimeStatusService.GetHealth())
      )
      .WithName("GetHealth")
      .WithTags("Runtime");

    return endpoints;
  }
}
