using System.Text.Encodings.Web;
using System.Text.Json;
using Dawning.AgentOS.Api.Results;
using Dawning.AgentOS.Application.Chat;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;
using Microsoft.AspNetCore.Http;

namespace Dawning.AgentOS.Api.Endpoints.Chat;

/// <summary>
/// Registers the chat endpoint group on the application's
/// <see cref="IEndpointRouteBuilder"/>. Per ADR-032 §决策 G1 V0 surfaces
/// four endpoints under <c>/api/chat</c>:
/// <list type="number">
///   <item>
///     <description>
///       <c>POST /api/chat/sessions</c> — create a new empty session;
///       returns the session snapshot so the renderer can start sending
///       messages immediately.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>GET  /api/chat/sessions</c> — paged list of sessions ordered
///       by <c>updated_at DESC</c>; default limit 50, max 200.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>GET  /api/chat/sessions/{id:guid}/messages</c> — full
///       persisted history of a session in send order; 404 with code
///       <c>chat.sessionNotFound</c> when the id is unknown.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>POST /api/chat/sessions/{id:guid}/messages</c> — send a new
///       user turn; the response is an SSE stream of <c>chunk</c> /
///       <c>done</c> / <c>error</c> events per ADR-032 §决策 H1.
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Auth is enforced by <see cref="Middleware.StartupTokenMiddleware"/>
/// before routing (ADR-023 §8 / ADR-032 §决策 K1 — startup token only).
/// For the create / list / messages-list endpoints, Result→HTTP
/// mapping for success cases goes through
/// <see cref="ResultHttpExtensions.ToHttpResult{T}(Result{T})"/>. The
/// not-found path needs <c>chat.sessionNotFound</c> → 404 which the
/// shared mapper does not provide, so the endpoints map errors manually
/// per ADR-032 §决策 K1.
/// </para>
/// <para>
/// The streaming endpoint deviates from <c>ToHttpResult</c> entirely:
/// validation failures return 400 ProblemDetails before any SSE
/// headers are written; once the headers ship the response is locked
/// into <c>text/event-stream</c> and per-frame errors are reported via
/// an <c>event: error</c> frame.
/// </para>
/// </remarks>
public static class ChatEndpoints
{
    /// <summary>Per ADR-032 §决策 G1 default page size for <c>GET /api/chat/sessions</c>.</summary>
    public const int DefaultListLimit = 50;

    /// <summary>Per ADR-032 §决策 G1 default offset for <c>GET /api/chat/sessions</c>.</summary>
    public const int DefaultListOffset = 0;

    private static readonly JsonSerializerOptions s_sseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Default Web defaults escape every non-ASCII character as \uXXXX,
        // which keeps the wire ASCII-safe but bloats Chinese deltas by
        // ~6× and makes the SSE stream unreadable in dev tools. The
        // chat surface only emits server-controlled JSON, never user-
        // supplied HTML, so the relaxed encoder is safe here.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Maps the chat endpoint group under <c>/api/chat</c>.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="routes"/> is null.</exception>
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/chat");

        // POST /api/chat/sessions — create empty session.
        group.MapPost(
            "/sessions",
            async (IChatAppService appService, CancellationToken cancellationToken) =>
            {
                var result = await appService
                    .CreateSessionAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return MapNonStreamingError(result.Errors);
                }

                return TypedResults.Ok(new CreateChatSessionResponse(result.Value));
            }
        );

        // GET /api/chat/sessions?limit=&offset= — list sessions.
        group.MapGet(
            "/sessions",
            async (
                IChatAppService appService,
                CancellationToken cancellationToken,
                int? limit,
                int? offset
            ) =>
            {
                var query = new ChatSessionListQuery(
                    Limit: limit ?? DefaultListLimit,
                    Offset: offset ?? DefaultListOffset
                );
                var result = await appService
                    .ListSessionsAsync(query, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return MapNonStreamingError(result.Errors);
                }

                return TypedResults.Ok(result.Value);
            }
        );

        // GET /api/chat/sessions/{id:guid}/messages — full history.
        group.MapGet(
            "/sessions/{id:guid}/messages",
            async (Guid id, IChatAppService appService, CancellationToken cancellationToken) =>
            {
                var result = await appService
                    .ListMessagesAsync(id, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return MapNonStreamingError(result.Errors);
                }

                return TypedResults.Ok(result.Value);
            }
        );

        // POST /api/chat/sessions/{id:guid}/messages — SSE stream.
        // Returning IResult here is deliberately avoided: the SSE writer
        // takes ownership of HttpContext.Response and ends the response
        // by hand. ASP.NET will treat the void return as "the endpoint
        // already wrote the response" and will not append anything.
        group.MapPost(
            "/sessions/{id:guid}/messages",
            async (
                Guid id,
                SendMessageRequest request,
                IChatAppService appService,
                HttpContext httpContext,
                CancellationToken cancellationToken
            ) =>
            {
                var result = await appService
                    .SendMessageStreamAsync(id, request, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return MapNonStreamingError(result.Errors);
                }

                await WriteSseStreamAsync(httpContext, result.Value, cancellationToken)
                    .ConfigureAwait(false);
                // Returning EmptyHttpResult here is purely a typing accommodation:
                // by the time we reach this line the body is already on the
                // wire, the headers are flushed, and the response is closed.
                // We use TypedResults rather than the unqualified Results
                // identifier because this file's namespace
                // (Dawning.AgentOS.Api.Endpoints.Chat) sees
                // Dawning.AgentOS.Api.Results shadow the BCL Results static.
                return (IResult)TypedResults.Empty;
            }
        );

        return routes;
    }

    /// <summary>
    /// Maps an Application-layer <see cref="DomainError"/> to the right
    /// HTTP status per ADR-032 §决策 K1: <c>chat.sessionNotFound</c>
    /// → 404, <c>llm.*</c> → ADR-028 §H1 table, field-level errors
    /// → 400, anything else → 422.
    /// </summary>
    private static IResult MapNonStreamingError(IReadOnlyList<DomainError> errors)
    {
        if (errors.Count == 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unknown failure",
                detail: "Result was a failure but carried no errors."
            );
        }

        var hasFieldError = false;
        foreach (var e in errors)
        {
            if (!string.IsNullOrWhiteSpace(e.Field))
            {
                hasFieldError = true;
                break;
            }
        }

        if (hasFieldError)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                detail: errors[0].Message,
                extensions: BuildErrorExtensions(errors)
            );
        }

        var head = errors[0];
        var statusCode = head.Code switch
        {
            ChatErrors.SessionNotFoundCode => StatusCodes.Status404NotFound,
            "llm.authenticationFailed" => StatusCodes.Status401Unauthorized,
            "llm.rateLimited" => StatusCodes.Status429TooManyRequests,
            "llm.upstreamUnavailable" => StatusCodes.Status502BadGateway,
            "llm.invalidRequest" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return TypedResults.Problem(
            statusCode: statusCode,
            title: head.Code,
            detail: head.Message,
            extensions: BuildErrorExtensions(errors)
        );
    }

    private static IDictionary<string, object?> BuildErrorExtensions(
        IReadOnlyList<DomainError> errors
    )
    {
        var entries = new ProblemErrorEntry[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            entries[i] = new ProblemErrorEntry(e.Code, e.Message, e.Field);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = errors[0].Code,
            ["errors"] = entries,
        };
    }

    /// <summary>
    /// Writes the supplied <see cref="LlmStreamChunk"/> sequence to
    /// <paramref name="httpContext"/> as an SSE response per ADR-032
    /// §决策 H1. Each chunk becomes exactly one SSE frame:
    /// <list type="bullet">
    ///   <item><description><c>Delta</c> → <c>event: chunk</c> + <c>data: {"delta":"..."}</c></description></item>
    ///   <item><description><c>Done</c>  → <c>event: done</c>  + <c>data: {"model":"...","promptTokens":N,"completionTokens":N,"durationMs":N}</c></description></item>
    ///   <item><description><c>Error</c> → <c>event: error</c> + <c>data: {"code":"...","message":"..."}</c></description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Note V0 deliberately omits <c>messageId</c> from the <c>done</c>
    /// frame; the renderer refetches <c>/api/chat/sessions/{id}/messages</c>
    /// after the stream ends if it needs the canonical assistant id.
    /// A future ADR may revisit this once tool-call ids are needed.
    /// </remarks>
    private static async Task WriteSseStreamAsync(
        HttpContext httpContext,
        IAsyncEnumerable<LlmStreamChunk> chunks,
        CancellationToken cancellationToken
    )
    {
        var response = httpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.Headers.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        // Defensive: some reverse proxies (nginx, IIS ARR) buffer output
        // by default. ADR-032 §决策 H1 requires we ship the
        // X-Accel-Buffering: no header so the proxy flushes per chunk.
        response.Headers["X-Accel-Buffering"] = "no";

        // ASP.NET Core enables chunked transfer encoding automatically
        // when no Content-Length is set; flushing after each frame is
        // what guarantees per-token visibility in the renderer.
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        await foreach (
            var chunk in chunks.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            switch (chunk.Kind)
            {
                case LlmStreamChunkKind.Delta:
                    await WriteSseFrameAsync(
                            response,
                            eventName: "chunk",
                            payload: new ChatSseChunkData(chunk.Delta ?? string.Empty),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    break;

                case LlmStreamChunkKind.Done:
                    await WriteSseFrameAsync(
                            response,
                            eventName: "done",
                            payload: new ChatSseDoneData(
                                Model: chunk.Model,
                                PromptTokens: chunk.PromptTokens,
                                CompletionTokens: chunk.CompletionTokens,
                                DurationMs: (long)chunk.Latency.TotalMilliseconds
                            ),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    break;

                case LlmStreamChunkKind.Error:
                    var error = chunk.Error;
                    await WriteSseFrameAsync(
                            response,
                            eventName: "error",
                            payload: new ChatSseErrorData(
                                Code: error?.Code ?? "llm.upstreamUnavailable",
                                Message: error?.Message ?? "Unknown streaming error."
                            ),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    break;

                default:
                    // Unknown kinds are forwarded verbatim as a chunk
                    // event with the raw delta, if any. This keeps the
                    // SSE wire stable when new chunk kinds are added by
                    // a future ADR without an immediate API revision.
                    await WriteSseFrameAsync(
                            response,
                            eventName: "chunk",
                            payload: new ChatSseChunkData(chunk.Delta ?? string.Empty),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    break;
            }
        }
    }

    private static async Task WriteSseFrameAsync<T>(
        HttpResponse response,
        string eventName,
        T payload,
        CancellationToken cancellationToken
    )
    {
        var json = JsonSerializer.Serialize(payload, s_sseJsonOptions);
        // SSE frame: 'event: NAME\n' + 'data: JSON\n' + '\n' (frame
        // terminator). 'data' is always a single line of JSON per
        // ADR-032 §决策 H1 — no embedded newlines to escape.
        var frame = $"event: {eventName}\ndata: {json}\n\n";
        await response.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>SSE <c>chunk</c> data payload.</summary>
    /// <param name="Delta">The text delta produced by the LLM since the previous chunk.</param>
    private sealed record ChatSseChunkData(string Delta);

    /// <summary>SSE <c>done</c> data payload.</summary>
    /// <param name="Model">The model identifier echoed by the provider.</param>
    /// <param name="PromptTokens">Tokens consumed by the prompt; <c>null</c> when unreported.</param>
    /// <param name="CompletionTokens">Tokens produced; <c>null</c> when unreported.</param>
    /// <param name="DurationMs">Wall-clock latency in milliseconds.</param>
    private sealed record ChatSseDoneData(
        string? Model,
        int? PromptTokens,
        int? CompletionTokens,
        long DurationMs
    );

    /// <summary>SSE <c>error</c> data payload.</summary>
    /// <param name="Code">The stable machine-readable error code.</param>
    /// <param name="Message">Human-readable error detail.</param>
    private sealed record ChatSseErrorData(string Code, string Message);

    /// <summary>RFC 7807 ProblemDetails extension entry mirroring <see cref="DomainError"/>.</summary>
    /// <param name="Code">The stable machine-readable error code.</param>
    /// <param name="Message">Human-readable detail.</param>
    /// <param name="Field">Field path for validation errors; <c>null</c> for business / streaming errors.</param>
    private sealed record ProblemErrorEntry(string Code, string Message, string? Field);
}
