using System.Security.Cryptography;
using System.Text;
using Dawning.AgentOS.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Api.Middleware;

/// <summary>
/// Rejects any request that does not present a matching startup token in
/// the configured header. Per ADR-017 every interaction with the local
/// backend has to carry the per-launch token shared with the Electron
/// shell; per ADR-023 §8 this middleware runs before <c>UseRouting</c>
/// so endpoints never execute in an unauthorized state.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="StartupTokenOptions.ExpectedToken"/> is empty the
/// middleware allows the request through. This open-mode is only intended
/// for the integration-test <c>WebApplicationFactory</c>; production hosts
/// always set a non-empty per-launch random value.
/// </para>
/// <para>
/// Token comparison uses <see cref="CryptographicOperations.FixedTimeEquals"/>
/// to avoid leaking timing differences. Failures emit a 401 with an
/// RFC 7807 ProblemDetails body via <see cref="Results.Problem(string?, string?, int?, string?, string?, IDictionary{string, object?})"/>.
/// </para>
/// </remarks>
public sealed class StartupTokenMiddleware(
    RequestDelegate next,
    IOptionsMonitor<StartupTokenOptions> optionsMonitor
)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly IOptionsMonitor<StartupTokenOptions> _optionsMonitor =
        optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrEmpty(options.ExpectedToken))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(options.HeaderName, out var presented))
        {
            await WriteUnauthorizedAsync(context, "Startup token header is missing.");
            return;
        }

        var presentedValue = presented.ToString();
        if (!FixedTimeEqualsString(presentedValue, options.ExpectedToken))
        {
            await WriteUnauthorizedAsync(context, "Startup token is invalid.");
            return;
        }

        await _next(context);
    }

    private static bool FixedTimeEqualsString(string a, string b)
    {
        if (a.Length != b.Length)
        {
            // Bail out immediately rather than attempting a fixed-time
            // compare on differently-sized buffers; the token length is
            // not itself a secret.
            return false;
        }

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string detail)
    {
        // TypedResults rather than Results because the project's own
        // `Dawning.AgentOS.Api.Results` namespace shadows the BCL
        // `Microsoft.AspNetCore.Http.Results` identifier.
        var problem = TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: detail
        );
        await problem.ExecuteAsync(context);
    }
}
