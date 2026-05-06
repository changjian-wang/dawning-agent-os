using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Infrastructure.Llm.OpenAi;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for <see cref="OpenAiLlmProvider"/> and, by delegation, the
/// shared <c>OpenAiCompatibleClient</c> H1 status-code mapping table
/// from ADR-028 §决策 H1. Each test installs a mocked
/// <see cref="HttpMessageHandler"/> that returns a canned upstream
/// response, then asserts the resulting <c>Result&lt;LlmCompletion&gt;</c>
/// surfaces the correct <c>llm.*</c> error code (or success).
/// </summary>
[TestFixture]
public class OpenAiLlmProviderTests
{
    private const string TestApiKey = "sk-unit-test";
    private const string TestBaseUrl = "https://api.openai.test/v1/";
    private const string TestModel = "gpt-4o-mini";

    [Test]
    public async Task CompleteAsync_OnSuccess_ReturnsCompletion()
    {
        const string body =
            "{ \"model\": \"gpt-4o-mini\", "
            + "\"choices\": [{ \"message\": { \"role\": \"assistant\", "
            + "\"content\": \"hello\" } }], "
            + "\"usage\": { \"prompt_tokens\": 3, \"completion_tokens\": 5 } }";
        var sut = BuildProvider(HttpStatusCode.OK, body);

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Content, Is.EqualTo("hello"));
        Assert.That(result.Value.Model, Is.EqualTo("gpt-4o-mini"));
        Assert.That(result.Value.PromptTokens, Is.EqualTo(3));
        Assert.That(result.Value.CompletionTokens, Is.EqualTo(5));
    }

    [Test]
    public async Task CompleteAsync_OnUnauthorized_ReturnsAuthenticationFailed()
    {
        var sut = BuildProvider(
            HttpStatusCode.Unauthorized,
            "{\"error\":{\"message\":\"bad key\"}}"
        );

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.authenticationFailed"));
    }

    [Test]
    public async Task CompleteAsync_OnTooManyRequests_ReturnsRateLimited()
    {
        var sut = BuildProvider(HttpStatusCode.TooManyRequests, "{}");

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.rateLimited"));
    }

    [Test]
    public async Task CompleteAsync_OnInternalServerError_ReturnsUpstreamUnavailable()
    {
        var sut = BuildProvider(HttpStatusCode.InternalServerError, "{}");

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.upstreamUnavailable"));
    }

    [Test]
    public async Task CompleteAsync_OnHttpRequestException_ReturnsUpstreamUnavailable()
    {
        var sut = BuildProviderWithHandler(handler =>
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("network down"))
        );

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.upstreamUnavailable"));
    }

    [Test]
    public void CompleteAsync_OnCancellation_PropagatesOperationCanceledException()
    {
        var sut = BuildProviderWithHandler(handler =>
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new OperationCanceledException())
        );

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // HttpClient may surface cancellation as TaskCanceledException
        // (a subclass of OperationCanceledException). CatchAsync matches
        // the base class, which is what ADR-028 §决策 H1 requires:
        // "the only failure mode allowed to propagate is
        // OperationCanceledException".
        Assert.CatchAsync<OperationCanceledException>(
            async () => await sut.CompleteAsync(BuildPingRequest(), cts.Token)
        );
    }

    [Test]
    public async Task CompleteAsync_WithEmptyApiKey_ReturnsAuthenticationFailedShortCircuit()
    {
        var sut = BuildProvider(HttpStatusCode.OK, "{}", apiKey: string.Empty);

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.authenticationFailed"));
    }

    [Test]
    public void ProviderName_ReturnsOpenAi()
    {
        var sut = BuildProvider(HttpStatusCode.OK, "{}");

        Assert.That(sut.ProviderName, Is.EqualTo(LlmOptions.OpenAiProviderName));
    }

    // -------- helpers --------

    private static LlmRequest BuildPingRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "ping")], Model: null, Temperature: null, MaxTokens: 8);

    private static OpenAiLlmProvider BuildProvider(
        HttpStatusCode status,
        string responseBody,
        string apiKey = TestApiKey
    ) =>
        BuildProviderWithHandler(
            handler =>
                handler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>()
                    )
                    .ReturnsAsync(
                        new HttpResponseMessage(status)
                        {
                            Content = new StringContent(
                                responseBody,
                                Encoding.UTF8,
                                "application/json"
                            ),
                        }
                    ),
            apiKey
        );

    private static OpenAiLlmProvider BuildProviderWithHandler(
        Action<Mock<HttpMessageHandler>> setupHandler,
        string apiKey = TestApiKey
    )
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        setupHandler(handler);

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri(TestBaseUrl) };
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(OpenAiLlmProvider.HttpClientName))
            .Returns(httpClient);

        var monitor = new Mock<IOptionsMonitor<LlmOptions>>();
        monitor
            .Setup(m => m.CurrentValue)
            .Returns(
                new LlmOptions
                {
                    ActiveProvider = LlmOptions.OpenAiProviderName,
                    Providers = new LlmProvidersOptions
                    {
                        OpenAI = new LlmProviderOptions
                        {
                            ApiKey = apiKey,
                            BaseUrl = TestBaseUrl,
                            Model = TestModel,
                        },
                    },
                }
            );

        return new OpenAiLlmProvider(factory.Object, monitor.Object);
    }
}
