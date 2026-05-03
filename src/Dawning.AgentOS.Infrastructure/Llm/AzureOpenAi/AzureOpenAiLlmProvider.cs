using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.Llm.AzureOpenAi;

/// <summary>
/// Azure OpenAI provider implementation. Per ADR-029 §决策 A1-I1,
/// Azure OpenAI's ChatCompletion endpoint is wire-compatible with OpenAI,
/// but differs in (1) URL shape (deployments/{id}/chat/completions),
/// and (2) auth (api-key header vs Bearer token). This provider resolves
/// those differences and delegates to the same JSON marshaling as OpenAI.
/// </summary>
internal sealed class AzureOpenAiLlmProvider : ILlmProvider
{
    /// <summary>
    /// Name of the registered <see cref="HttpClient"/> for Azure OpenAI.
    /// </summary>
    public const string HttpClientName = "llm-azure-openai";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LlmOptions> _options;

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>Initializes a new instance of <see cref="AzureOpenAiLlmProvider"/>.</summary>
    public AzureOpenAiLlmProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<LlmOptions> options
    )
    {
        _httpClientFactory =
            httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string ProviderName => LlmOptions.AzureOpenAiProviderName;

    /// <inheritdoc />
    public async Task<Result<LlmCompletion>> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Messages.Count == 0)
        {
            return Result<LlmCompletion>.Failure(
                LlmErrors.InvalidRequest("LlmRequest.Messages must contain at least one entry.")
            );
        }

        var azureOptions = _options.CurrentValue.Providers.AzureOpenAI;

        if (string.IsNullOrEmpty(azureOptions.ApiKey))
        {
            return Result<LlmCompletion>.Failure(
                LlmErrors.AuthenticationFailed(
                    "API key is not configured for the active LLM provider."
                )
            );
        }

        if (string.IsNullOrEmpty(azureOptions.Endpoint) || string.IsNullOrEmpty(azureOptions.DeploymentId))
        {
            return Result<LlmCompletion>.Failure(
                LlmErrors.InvalidRequest(
                    "Azure OpenAI endpoint and deployment ID must be configured."
                )
            );
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var deploymentId = azureOptions.DeploymentId;

        var body = new ChatCompletionRequestBody
        {
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

        // Azure OpenAI URL path: /openai/deployments/{deployment-id}/chat/completions
        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            $"/openai/deployments/{deploymentId}/chat/completions"
        )
        {
            Content = JsonContent.Create(body, options: s_jsonOptions),
        };

        // Azure uses api-key header instead of Bearer Authorization
        requestMessage.Headers.Add("api-key", azureOptions.ApiKey);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await httpClient
                .SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
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
                    Model: payload.Model ?? azureOptions.DeploymentId,
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
            // fall through
        }
        catch (NotSupportedException)
        {
            // fall through
        }

        var detail = string.IsNullOrWhiteSpace(upstreamMessage)
            ? $"Upstream responded with HTTP {(int)response.StatusCode}."
            : $"HTTP {(int)response.StatusCode}: {upstreamMessage}";

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                LlmErrors.AuthenticationFailed(detail),
            System.Net.HttpStatusCode.TooManyRequests => LlmErrors.RateLimited(detail),
            System.Net.HttpStatusCode.RequestTimeout => LlmErrors.UpstreamUnavailable(detail),
            >= System.Net.HttpStatusCode.InternalServerError => LlmErrors.UpstreamUnavailable(detail),
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

    // -------- wire DTOs --------

    private sealed class ChatCompletionRequestBody
    {
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
