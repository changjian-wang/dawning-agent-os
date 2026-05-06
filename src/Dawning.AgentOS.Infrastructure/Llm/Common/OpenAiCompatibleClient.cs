using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
                .SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken)
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
                    LlmErrors.UpstreamUnavailable(
                        $"Upstream returned malformed JSON: {ex.Message}"
                    )
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
}
