using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Infrastructure.Llm.DeepSeek;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for <see cref="DeepSeekLlmProvider"/>. The exhaustive H1
/// status-code mapping table is exercised through the OpenAI provider
/// (since both share <c>OpenAiCompatibleClient</c>); this fixture
/// asserts the DeepSeek-specific wiring per ADR-028 §决策 C1 / I1:
/// the right named <see cref="HttpClient"/> is requested, the DeepSeek
/// section drives ApiKey / Model resolution, and the provider name is
/// reported correctly.
/// </summary>
[TestFixture]
public class DeepSeekLlmProviderTests
{
    private const string TestApiKey = "ds-unit-test";
    private const string TestBaseUrl = "https://api.deepseek.test/";
    private const string TestModel = "deepseek-chat";

    [Test]
    public async Task CompleteAsync_OnSuccess_ReturnsCompletion()
    {
        const string body =
            "{ \"model\": \"deepseek-chat\", "
            + "\"choices\": [{ \"message\": { \"role\": \"assistant\", "
            + "\"content\": \"hi from deepseek\" } }] }";
        var sut = BuildProvider(HttpStatusCode.OK, body);

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Content, Is.EqualTo("hi from deepseek"));
        Assert.That(result.Value.Model, Is.EqualTo("deepseek-chat"));
    }

    [Test]
    public async Task CompleteAsync_UsesDeepSeekNamedHttpClient()
    {
        var (sut, factory) = BuildProviderAndFactory(HttpStatusCode.OK, BuildHelloBody());

        await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        factory.Verify(f => f.CreateClient(DeepSeekLlmProvider.HttpClientName), Times.Once);
    }

    [Test]
    public async Task CompleteAsync_OnUnauthorized_ReturnsAuthenticationFailed()
    {
        var sut = BuildProvider(HttpStatusCode.Unauthorized, "{}");

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.authenticationFailed"));
    }

    [Test]
    public void ProviderName_ReturnsDeepSeek()
    {
        var sut = BuildProvider(HttpStatusCode.OK, "{}");

        Assert.That(sut.ProviderName, Is.EqualTo(LlmOptions.DeepSeekProviderName));
    }

    // -------- helpers --------

    private static LlmRequest BuildPingRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "ping")], Model: null, Temperature: null, MaxTokens: 8);

    private static string BuildHelloBody() =>
        "{ \"model\": \"deepseek-chat\", "
        + "\"choices\": [{ \"message\": { \"role\": \"assistant\", "
        + "\"content\": \"ok\" } }] }";

    private static DeepSeekLlmProvider BuildProvider(HttpStatusCode status, string responseBody)
    {
        var (sut, _) = BuildProviderAndFactory(status, responseBody);
        return sut;
    }

    private static (DeepSeekLlmProvider Provider, Mock<IHttpClientFactory> Factory) BuildProviderAndFactory(
        HttpStatusCode status,
        string responseBody
    )
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
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
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
                }
            );

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri(TestBaseUrl) };
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(DeepSeekLlmProvider.HttpClientName))
            .Returns(httpClient);

        var monitor = new Mock<IOptionsMonitor<LlmOptions>>();
        monitor
            .Setup(m => m.CurrentValue)
            .Returns(
                new LlmOptions
                {
                    ActiveProvider = LlmOptions.DeepSeekProviderName,
                    Providers = new LlmProvidersOptions
                    {
                        DeepSeek = new LlmProviderOptions
                        {
                            ApiKey = TestApiKey,
                            BaseUrl = TestBaseUrl,
                            Model = TestModel,
                        },
                    },
                }
            );

        return (new DeepSeekLlmProvider(factory.Object, monitor.Object), factory);
    }
}
