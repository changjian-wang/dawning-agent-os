using System.Diagnostics;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Llm;
using Microsoft.AspNetCore.Http;

namespace Dawning.AgentOS.Api.Endpoints.Llm;

/// <summary>
/// Registers the LLM smoke endpoint group on the application's
/// <see cref="IEndpointRouteBuilder"/>. Per ADR-028 §决策 D1 V0
/// surfaces exactly one endpoint:
/// <list type="number">
///   <item>
///     <description>
///       <c>GET /api/llm/ping</c> — issues a single 8-token chat
///       completion to the active provider and returns provider /
///       model / content / wall-clock duration. Used by smoke probes
///       and dogfood verification; not consumed by the renderer.
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Auth is enforced by <c>StartupTokenMiddleware</c> before routing
/// (ADR-023 §8); the endpoint cannot run without the matching
/// <c>X-Startup-Token</c>. Errors are surfaced as RFC 7807
/// ProblemDetails with the underlying <c>llm.*</c> code in the
/// <c>code</c> field, mirroring inbox endpoints.
/// </remarks>
public static class LlmEndpoints
{
    /// <summary>
    /// Maps the LLM endpoint group under <c>/api/llm</c>.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="routes"/> is null.</exception>
    public static IEndpointRouteBuilder MapLlmEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/llm");

        group.MapGet(
            "/ping",
            async (ILlmProvider provider, CancellationToken cancellationToken) =>
            {
                // ADR-028 §决策 D1: 1-message ping with a hard
                // 8-token cap so even mis-configured upstreams
                // bound the cost of a smoke probe.
                var request = new LlmRequest(
                    Messages: [new LlmMessage(LlmRole.User, "ping")],
                    Model: null,
                    Temperature: null,
                    MaxTokens: 8
                );

                var stopwatch = Stopwatch.StartNew();
                var result = await provider
                    .CompleteAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                stopwatch.Stop();

                if (result.IsSuccess)
                {
                    return (IResult)
                        TypedResults.Ok(
                            new LlmPingResponse(
                                Provider: provider.ProviderName,
                                Model: result.Value.Model,
                                Content: result.Value.Content,
                                DurationMs: (long)stopwatch.Elapsed.TotalMilliseconds,
                                PromptTokens: result.Value.PromptTokens,
                                CompletionTokens: result.Value.CompletionTokens
                            )
                        );
                }

                var error = result.Errors[0];
                var statusCode = error.Code switch
                {
                    "llm.authenticationFailed" => StatusCodes.Status401Unauthorized,
                    "llm.rateLimited" => StatusCodes.Status429TooManyRequests,
                    "llm.upstreamUnavailable" => StatusCodes.Status502BadGateway,
                    "llm.invalidRequest" => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError,
                };

                return TypedResults.Problem(
                    statusCode: statusCode,
                    title: error.Code,
                    detail: error.Message,
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["code"] = error.Code,
                        ["provider"] = provider.ProviderName,
                    }
                );
            }
        );

        return routes;
    }

    /// <summary>
    /// Response shape for <c>GET /api/llm/ping</c>. Per ADR-028
    /// §决策 D1 the field set is intentionally narrow: provider /
    /// model / content for human inspection plus token counts and a
    /// wall-clock <c>durationMs</c> for diagnostics.
    /// </summary>
    /// <param name="Provider">The provider name (<c>"OpenAI"</c> or <c>"DeepSeek"</c>).</param>
    /// <param name="Model">Model identifier echoed by the provider.</param>
    /// <param name="Content">The assistant's response text.</param>
    /// <param name="DurationMs">Wall-clock latency including local overhead, in milliseconds.</param>
    /// <param name="PromptTokens">Tokens consumed by the prompt; null when unreported.</param>
    /// <param name="CompletionTokens">Tokens produced; null when unreported.</param>
    private sealed record LlmPingResponse(
        string Provider,
        string Model,
        string Content,
        long DurationMs,
        int? PromptTokens,
        int? CompletionTokens
    );
}
