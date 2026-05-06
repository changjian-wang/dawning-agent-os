using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Infrastructure.Llm.Common;

/// <summary>
/// Shared HTTP + JSON adapter for any provider whose chat-completion
/// endpoint is wire-compatible with OpenAI's
/// <c>POST {BaseUrl}/chat/completions</c>. Per ADR-028 §决策 C1 the
/// V0 OpenAI and DeepSeek providers both delegate here; per-provider
/// adapters supply only the named <see cref="HttpClient"/>, the
/// resolved API key, and the resolved default model.
/// </summary>
/// <remarks>
/// <para>
/// Errors are translated to <see cref="LlmErrors"/> per the H1 mapping
/// table in ADR-028 §决策 H1; the only exception this method allows to
/// propagate is <see cref="OperationCanceledException"/> raised by the
/// caller's <see cref="CancellationToken"/>.
/// </para>
/// <para>
/// JSON shape (per OpenAI ChatCompletion):
/// <list type="bullet">
///   <item><description>request body: <c>{ model, messages, temperature?, max_tokens? }</c></description></item>
///   <item><description>success body: <c>{ model, choices[0].message.content, usage.prompt_tokens?, usage.completion_tokens? }</c></description></item>
///   <item><description>error body (4xx/5xx): <c>{ error: { message, type, code } }</c></description></item>
/// </list>
/// </para>
/// </remarks>
internal static class OpenAiCompatibleClient
{
    private const string ChatCompletionsPath = "chat/completions";
    private const string AuthorizationScheme = "Bearer";

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Executes a single chat completion call against the configured
    /// upstream and translates the response into a
    /// <see cref="Result{T}"/> per the H1 mapping table.
    /// </summary>
    /// <param name="httpClient">The named HTTP client whose <c>BaseAddress</c> is the provider's base URL.</param>
    /// <param name="apiKey">The API key; empty string short-circuits to <see cref="LlmErrors.AuthenticationFailed(string)"/>.</param>
    /// <param name="defaultModel">The model used when <see cref="LlmRequest.Model"/> is null.</param>
    /// <param name="request">The caller-supplied chat request.</param>
    /// <param name="cancellationToken">Cooperative cancellation token; flowed end-to-end.</param>
    /// <returns>An <see cref="LlmCompletion"/> on success, or a mapped <see cref="LlmErrors"/> failure.</returns>
    public static async Task<Result<LlmCompletion>> CompleteAsync(
        HttpClient httpClient,
        string apiKey,
        string defaultModel,
        LlmRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(defaultModel);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Messages.Count == 0)
        {
            return Result<LlmCompletion>.Failure(
                LlmErrors.InvalidRequest("LlmRequest.Messages must contain at least one entry.")
            );
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return Result<LlmCompletion>.Failure(
                LlmErrors.AuthenticationFailed(
                    "API key is not configured for the active LLM provider."
                )
            );
        }

        var body = new ChatCompletionRequestBody
        {
            Model = string.IsNullOrEmpty(request.Model) ? defaultModel : request.Model,
            Messages = request
                .Messages.Select(m => new ChatCompletionMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content,
                })
                .ToArray(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsPath)
        {
            Content = JsonContent.Create(body, options: s_jsonOptions),
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            AuthorizationScheme,
            apiKey
        );

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await httpClient
                .SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseContentRead,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Per ADR-028 §决策 H1 cancellation is the only failure
            // mode allowed to propagate; let it bubble up.
            throw;
        }
        catch (HttpRequestException ex)
        {
            return Result<LlmCompletion>.Failure(
                LlmErrors.UpstreamUnavailable($"HTTP request failed: {ex.Message}")
            );
        }

        try
        {
            stopwatch.Stop();
            var latency = stopwatch.Elapsed;

            if (!response.IsSuccessStatusCode)
            {
                return Result<LlmCompletion>.Failure(
                    await TranslateErrorAsync(response, cancellationToken).ConfigureAwait(false)
                );
            }

            ChatCompletionResponseBody? payload;
            try
            {
                payload = await response
                    .Content.ReadFromJsonAsync<ChatCompletionResponseBody>(
                        s_jsonOptions,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return Result<LlmCompletion>.Failure(
                    LlmErrors.UpstreamUnavailable($"Upstream returned malformed JSON: {ex.Message}")
                );
            }

            if (payload is null)
            {
                return Result<LlmCompletion>.Failure(
                    LlmErrors.UpstreamUnavailable("Upstream returned an empty response body.")
                );
            }

            var firstChoice = payload.Choices?.FirstOrDefault();
            var content = firstChoice?.Message?.Content;
            if (firstChoice is null || content is null)
            {
                return Result<LlmCompletion>.Failure(
                    LlmErrors.UpstreamUnavailable(
                        "Upstream response did not contain a choices[0].message.content field."
                    )
                );
            }

            return Result<LlmCompletion>.Success(
                new LlmCompletion(
                    Content: content,
                    Model: string.IsNullOrEmpty(payload.Model) ? body.Model : payload.Model,
                    PromptTokens: payload.Usage?.PromptTokens,
                    CompletionTokens: payload.Usage?.CompletionTokens,
                    Latency: latency
                )
            );
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Executes a streaming chat completion call against the configured
    /// upstream and yields each upstream event as an
    /// <see cref="LlmStreamChunk"/>. Per ADR-032 §决策 F1 the stream is
    /// terminated by exactly one terminal chunk
    /// (<see cref="LlmStreamChunkKind.Done"/> on success or
    /// <see cref="LlmStreamChunkKind.Error"/> on failure).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wire shape (OpenAI SSE): each event is a <c>data: {json}\n\n</c>
    /// frame; <c>data: [DONE]\n\n</c> signals end-of-stream. Each JSON
    /// frame contains <c>choices[0].delta.content</c> for text deltas
    /// and optionally a top-level <c>usage</c> object on the frame
    /// immediately preceding <c>[DONE]</c> when
    /// <c>stream_options.include_usage</c> is set.
    /// </para>
    /// <para>
    /// HTTP errors before the stream starts are translated through the
    /// same H1 mapping table as <see cref="CompleteAsync"/>; once the
    /// stream begins, JSON parse failures or unexpected EOF surface as
    /// a single <see cref="LlmStreamChunkKind.Error"/> chunk with
    /// <c>llm.upstreamUnavailable</c>. Cancellation is the only failure
    /// mode allowed to propagate.
    /// </para>
    /// </remarks>
    public static async IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
        HttpClient httpClient,
        string apiKey,
        string defaultModel,
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(defaultModel);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Messages.Count == 0)
        {
            yield return LlmStreamChunk.ForError(
                LlmErrors.InvalidRequest("LlmRequest.Messages must contain at least one entry.")
            );
            yield break;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            yield return LlmStreamChunk.ForError(
                LlmErrors.AuthenticationFailed(
                    "API key is not configured for the active LLM provider."
                )
            );
            yield break;
        }

        var body = new ChatCompletionRequestBody
        {
            Model = string.IsNullOrEmpty(request.Model) ? defaultModel : request.Model,
            Messages = request
                .Messages.Select(m => new ChatCompletionMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content,
                })
                .ToArray(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = true,
            // include_usage gives prompt/completion token counts on the
            // final frame before [DONE]; documented in OpenAI's
            // streaming guide and silently ignored by providers that
            // don't support it.
            StreamOptions = new ChatCompletionStreamOptions { IncludeUsage = true },
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsPath)
        {
            Content = JsonContent.Create(body, options: s_jsonOptions),
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            AuthorizationScheme,
            apiKey
        );

        await foreach (
            var chunk in StreamFromRequestAsync(
                    httpClient,
                    requestMessage,
                    body.Model,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Executes the supplied pre-built <see cref="HttpRequestMessage"/>
    /// against <paramref name="httpClient"/> and projects the SSE body
    /// into <see cref="LlmStreamChunk"/>s. Shared with the Azure OpenAI
    /// provider, which has a different URL shape and auth header but
    /// the same SSE wire format.
    /// </summary>
    public static async IAsyncEnumerable<LlmStreamChunk> StreamFromRequestAsync(
        HttpClient httpClient,
        HttpRequestMessage requestMessage,
        string fallbackModel,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestMessage);
        ArgumentNullException.ThrowIfNull(fallbackModel);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        DomainError? sendError = null;
        try
        {
            response = await httpClient
                .SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            sendError = LlmErrors.UpstreamUnavailable($"HTTP request failed: {ex.Message}");
        }

        if (sendError is not null)
        {
            yield return LlmStreamChunk.ForError(sendError);
            yield break;
        }

        try
        {
            if (!response!.IsSuccessStatusCode)
            {
                yield return LlmStreamChunk.ForError(
                    await TranslateErrorAsync(response, cancellationToken).ConfigureAwait(false)
                );
                yield break;
            }

            await foreach (
                var chunk in ReadSseChunksAsync(
                        response,
                        fallbackModel,
                        stopwatch,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                yield return chunk;
            }
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static async IAsyncEnumerable<LlmStreamChunk> ReadSseChunksAsync(
        HttpResponseMessage response,
        string fallbackModel,
        Stopwatch stopwatch,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        Stream? contentStream = null;
        DomainError? openError = null;
        try
        {
            contentStream = await response
                .Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            openError = LlmErrors.UpstreamUnavailable(
                $"Failed to open response stream: {ex.Message}"
            );
        }

        if (openError is not null)
        {
            yield return LlmStreamChunk.ForError(openError);
            yield break;
        }

        await using var scopedStream = contentStream!.ConfigureAwait(false);
        using var reader = new StreamReader(contentStream!);

        string? finalModel = null;
        int? finalPromptTokens = null;
        int? finalCompletionTokens = null;
        var emittedTerminal = false;

        while (true)
        {
            string? line;
            DomainError? readError = null;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException ex)
            {
                line = null;
                readError = LlmErrors.UpstreamUnavailable(
                    $"Stream read failed mid-flight: {ex.Message}"
                );
            }

            if (readError is not null)
            {
                if (!emittedTerminal)
                {
                    yield return LlmStreamChunk.ForError(readError);
                    emittedTerminal = true;
                }

                yield break;
            }

            if (line is null)
            {
                // Server closed the stream without sending [DONE]. Treat
                // it as a clean end-of-stream so the caller still gets a
                // terminal Done chunk with the most recent usage data.
                if (!emittedTerminal)
                {
                    stopwatch.Stop();
                    yield return LlmStreamChunk.ForDone(
                        model: finalModel ?? fallbackModel,
                        promptTokens: finalPromptTokens,
                        completionTokens: finalCompletionTokens,
                        latency: stopwatch.Elapsed
                    );
                    emittedTerminal = true;
                }

                yield break;
            }

            if (line.Length == 0)
            {
                // Inter-event blank line; skip.
                continue;
            }

            // SSE comments start with ':' (e.g. OpenAI keep-alive pings).
            if (line[0] == ':')
            {
                continue;
            }

            const string DataPrefix = "data:";
            if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                // Unexpected non-data line (e.g. event:, id:); ignore.
                continue;
            }

            var payload = line[DataPrefix.Length..].TrimStart();

            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                stopwatch.Stop();
                if (!emittedTerminal)
                {
                    yield return LlmStreamChunk.ForDone(
                        model: finalModel ?? fallbackModel,
                        promptTokens: finalPromptTokens,
                        completionTokens: finalCompletionTokens,
                        latency: stopwatch.Elapsed
                    );
                    emittedTerminal = true;
                }

                yield break;
            }

            ChatCompletionStreamFrame? frame;
            DomainError? parseError = null;
            try
            {
                frame = JsonSerializer.Deserialize<ChatCompletionStreamFrame>(
                    payload,
                    s_jsonOptions
                );
            }
            catch (JsonException ex)
            {
                frame = null;
                parseError = LlmErrors.UpstreamUnavailable(
                    $"Upstream returned malformed SSE JSON: {ex.Message}"
                );
            }

            if (parseError is not null)
            {
                if (!emittedTerminal)
                {
                    yield return LlmStreamChunk.ForError(parseError);
                    emittedTerminal = true;
                }

                yield break;
            }

            if (frame is null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(frame.Model))
            {
                finalModel = frame.Model;
            }

            if (frame.Usage is not null)
            {
                finalPromptTokens = frame.Usage.PromptTokens ?? finalPromptTokens;
                finalCompletionTokens = frame.Usage.CompletionTokens ?? finalCompletionTokens;
            }

            var deltaContent = frame.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(deltaContent))
            {
                yield return LlmStreamChunk.ForDelta(deltaContent);
            }
        }
    }

    private static async Task<DomainError> TranslateErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        string? upstreamMessage = null;
        try
        {
            var error = await response
                .Content.ReadFromJsonAsync<ChatCompletionErrorEnvelope>(
                    s_jsonOptions,
                    cancellationToken
                )
                .ConfigureAwait(false);
            upstreamMessage = error?.Error?.Message;
        }
        catch (JsonException)
        {
            // fall through; some providers return non-JSON 5xx bodies.
        }
        catch (NotSupportedException)
        {
            // unsupported content type; fall through.
        }

        var detail = string.IsNullOrWhiteSpace(upstreamMessage)
            ? $"Upstream responded with HTTP {(int)response.StatusCode}."
            : $"HTTP {(int)response.StatusCode}: {upstreamMessage}";

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                LlmErrors.AuthenticationFailed(detail),
            HttpStatusCode.TooManyRequests => LlmErrors.RateLimited(detail),
            HttpStatusCode.RequestTimeout => LlmErrors.UpstreamUnavailable(detail),
            >= HttpStatusCode.InternalServerError => LlmErrors.UpstreamUnavailable(detail),
            _ => LlmErrors.InvalidRequest(detail),
        };
    }

    private static string MapRole(LlmRole role) =>
        role switch
        {
            LlmRole.System => "system",
            LlmRole.User => "user",
            LlmRole.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown LlmRole."),
        };

    // -------- wire DTOs (snake_case via SnakeCaseLower naming policy) --------

    private sealed class ChatCompletionRequestBody
    {
        public string Model { get; set; } = string.Empty;
        public IReadOnlyList<ChatCompletionMessage> Messages { get; set; } = [];
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public bool? Stream { get; set; }
        public ChatCompletionStreamOptions? StreamOptions { get; set; }
    }

    private sealed class ChatCompletionStreamOptions
    {
        public bool? IncludeUsage { get; set; }
    }

    private sealed class ChatCompletionMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponseBody
    {
        public string? Model { get; set; }
        public IReadOnlyList<ChatCompletionChoice>? Choices { get; set; }
        public ChatCompletionUsage? Usage { get; set; }
    }

    private sealed class ChatCompletionChoice
    {
        public ChatCompletionMessage? Message { get; set; }
    }

    private sealed class ChatCompletionUsage
    {
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
    }

    private sealed class ChatCompletionErrorEnvelope
    {
        public ChatCompletionErrorBody? Error { get; set; }
    }

    private sealed class ChatCompletionErrorBody
    {
        public string? Message { get; set; }
    }

    // -------- streaming wire DTOs --------

    private sealed class ChatCompletionStreamFrame
    {
        public string? Model { get; set; }
        public IReadOnlyList<ChatCompletionStreamChoice>? Choices { get; set; }
        public ChatCompletionUsage? Usage { get; set; }
    }

    private sealed class ChatCompletionStreamChoice
    {
        public ChatCompletionStreamDelta? Delta { get; set; }
    }

    private sealed class ChatCompletionStreamDelta
    {
        public string? Content { get; set; }
    }
}
