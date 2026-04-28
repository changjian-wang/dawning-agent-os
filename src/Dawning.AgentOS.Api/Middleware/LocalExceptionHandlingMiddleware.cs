namespace Dawning.AgentOS.Api.Middleware;

public sealed class LocalExceptionHandlingMiddleware(
  RequestDelegate next,
  ILogger<LocalExceptionHandlingMiddleware> logger
)
{
  public async Task InvokeAsync(HttpContext context)
  {
    try
    {
      await next(context);
    }
    catch (Exception exception)
    {
      logger.LogError(exception, "Unhandled local API exception.");
      context.Response.StatusCode = StatusCodes.Status500InternalServerError;
      await context.Response.WriteAsJsonAsync(
        new { error = "local_api_error", traceId = context.TraceIdentifier },
        context.RequestAborted
      );
    }
  }
}
