using Dawning.AgentOS.Application.Runtime;

namespace Dawning.AgentOS.Api.Endpoints;

public static class RuntimeEndpoints
{
  public static IEndpointRouteBuilder MapRuntimeEndpoints(this IEndpointRouteBuilder endpoints)
  {
    endpoints
      .MapGet(
        "/runtime/status",
        (IRuntimeStatusService runtimeStatusService) => Results.Ok(runtimeStatusService.GetStatus())
      )
      .WithName("GetRuntimeStatus")
      .WithTags("Runtime");

    return endpoints;
  }
}
