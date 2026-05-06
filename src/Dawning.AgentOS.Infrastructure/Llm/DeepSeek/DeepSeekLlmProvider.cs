using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Infrastructure.Llm.Common;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.Llm.DeepSeek;

/// <summary>
/// DeepSeek provider implementation. Per ADR-028 §决策 C1 DeepSeek's
/// chat-completion endpoint is wire-compatible with OpenAI's, so the
/// adapter shares <see cref="OpenAiCompatibleClient"/> with OpenAI; the
/// only differences are the named <see cref="HttpClient"/>'s base URL
/// and the configured key. The compatibility assumption is documented
/// in ADR-028 §决策 C1 and re-verified by the unit-test suite.
/// </summary>
internal sealed class DeepSeekLlmProvider : ILlmProvider
{
    /// <summary>
    /// Name of the registered <see cref="HttpClient"/> the DI factory
    /// hands out for DeepSeek calls. Per ADR-028 §决策 I1 each provider
    /// gets a separate named client.
    /// </summary>
    public const string HttpClientName = "llm-deepseek";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LlmOptions> _options;

    /// <summary>Initializes a new instance of <see cref="DeepSeekLlmProvider"/>.</summary>
    public DeepSeekLlmProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<LlmOptions> options
    )
    {
        _httpClientFactory =
            httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string ProviderName => LlmOptions.DeepSeekProviderName;

    /// <inheritdoc />
    public Task<Result<LlmCompletion>> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerOptions = _options.CurrentValue.Providers.DeepSeek;
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        return OpenAiCompatibleClient.CompleteAsync(
            httpClient,
            providerOptions.ApiKey,
            providerOptions.Model,
            request,
            cancellationToken
        );
    }
}
