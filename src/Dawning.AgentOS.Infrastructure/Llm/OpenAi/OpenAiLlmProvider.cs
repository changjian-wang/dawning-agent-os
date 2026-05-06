using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Infrastructure.Llm.Common;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.Llm.OpenAi;

/// <summary>
/// OpenAI provider implementation. Per ADR-028 §决策 C1 the actual HTTP
/// + JSON dance lives in <see cref="OpenAiCompatibleClient"/>; this
/// adapter only resolves the named <see cref="HttpClient"/> and the
/// per-provider option bag, then delegates.
/// </summary>
internal sealed class OpenAiLlmProvider : ILlmProvider
{
    /// <summary>
    /// Name of the registered <see cref="HttpClient"/> the DI factory
    /// hands out for OpenAI calls. Per ADR-028 §决策 I1 each provider
    /// gets a separate named client so the <c>BaseAddress</c> binding is
    /// stable across calls.
    /// </summary>
    public const string HttpClientName = "llm-openai";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LlmOptions> _options;

    /// <summary>Initializes a new instance of <see cref="OpenAiLlmProvider"/>.</summary>
    public OpenAiLlmProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<LlmOptions> options
    )
    {
        _httpClientFactory =
            httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string ProviderName => LlmOptions.OpenAiProviderName;

    /// <inheritdoc />
    public Task<Result<LlmCompletion>> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerOptions = _options.CurrentValue.Providers.OpenAI;
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        return OpenAiCompatibleClient.CompleteAsync(
            httpClient,
            providerOptions.ApiKey,
            providerOptions.Model,
            request,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
        LlmRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerOptions = _options.CurrentValue.Providers.OpenAI;
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        return OpenAiCompatibleClient.CompleteStreamAsync(
            httpClient,
            providerOptions.ApiKey,
            providerOptions.Model,
            request,
            cancellationToken
        );
    }
}
