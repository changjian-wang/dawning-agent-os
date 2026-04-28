using Dawning.AgentOS.Infrastructure.Security;

namespace Dawning.AgentOS.Api.Middleware;

public sealed class StartupTokenMiddleware(RequestDelegate next)
{
  public async Task InvokeAsync(HttpContext context, IStartupTokenValidator startupTokenValidator)
  {
    var token = context.Request.Headers[StartupTokenDefaults.HeaderName].FirstOrDefault();
    if (!startupTokenValidator.IsValid(token))
    {
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;
      await context.Response.WriteAsJsonAsync(
        new { error = "startup_token_required", traceId = context.TraceIdentifier },
        context.RequestAborted
      );
      return;
    }

    await next(context);
  }
}
