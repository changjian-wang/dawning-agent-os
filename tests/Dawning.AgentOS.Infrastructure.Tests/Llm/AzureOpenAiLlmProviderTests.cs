using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Infrastructure.Llm.AzureOpenAi;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for <see cref="AzureOpenAiLlmProvider"/>. Per ADR-029, Azure OpenAI
/// differs from standard OpenAI in (1) URL construction (deployments/{id}/chat/completions),
/// (2) auth header (api-key vs Bearer), and (3) configuration (endpoint + deployment
/// rather than base URL + model). This fixture exercises those differences while
/// reusing the H1 error mapping from ADR-028.
/// </summary>
[TestFixture]
public class AzureOpenAiLlmProviderTests
{
    private const string TestApiKey = "azure-unit-test-key";
    private const string TestEndpoint = "https://my-resource.openai.azure.com";
    private const string TestDeploymentId = "gpt-4";

    [Test]
    public async Task CompleteAsync_OnSuccess_ReturnsCompletion()
    {
        const string body =
            "{ \"model\": \"gpt-4\", "
            + "\"choices\": [{ \"message\": { \"role\": \"assistant\", "
            + "\"content\": \"azure response\" } }], "
            + "\"usage\": { \"prompt_tokens\": 2, \"completion_tokens\": 3 } }";
        var sut = BuildProvider(HttpStatusCode.OK, body);

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Content, Is.EqualTo("azure response"));
        Assert.That(result.Value.PromptTokens, Is.EqualTo(2));
        Assert.That(result.Value.CompletionTokens, Is.EqualTo(3));
    }

    [Test]
    public async Task CompleteAsync_UsesApiKeyHeader()
    {
        var (sut, factory, handler) = BuildProviderWithHandler(
            HttpStatusCode.OK,
            BuildHelloBody()
        );

        await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        // Verify api-key header was sent (not Bearer)
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg =>
                msg.Headers.Contains("api-key")
                && msg.Headers.GetValues("api-key").First() == TestApiKey
                && !msg.Headers.Contains("Authorization")
            ),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Test]
    public async Task CompleteAsync_ConstructsAzureUrlPath()
    {
        var (sut, _, handler) = BuildProviderWithHandler(HttpStatusCode.OK, BuildHelloBody());

        await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        // Verify URL path contains deployment ID
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg =>
                msg.RequestUri!.ToString().Contains($"/openai/deployments/{TestDeploymentId}")
            ),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Test]
    public async Task CompleteAsync_AppendsApiVersionQueryParameter()
    {
        var (sut, _, handler) = BuildProviderWithHandler(HttpStatusCode.OK, BuildHelloBody());

        await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        // Per ADR-029, Azure mandates the ?api-version=... query parameter.
        handler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg =>
                    msg.RequestUri!.Query.Contains(
                        $"api-version={LlmAzureOpenAiProviderOptions.DefaultApiVersion}",
                        StringComparison.Ordinal
                    )
                ),
                ItExpr.IsAny<CancellationToken>()
            );
    }

    [Test]
    public async Task CompleteAsync_HonorsCustomApiVersion()
    {
        const string customApiVersion = "2024-08-01-preview";
        var (sut, _, handler) = BuildProviderWithHandler(
            HttpStatusCode.OK,
            BuildHelloBody(),
            apiVersion: customApiVersion
        );

        await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        handler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg =>
                    msg.RequestUri!.Query.Contains(
                        $"api-version={customApiVersion}",
                        StringComparison.Ordinal
                    )
                ),
                ItExpr.IsAny<CancellationToken>()
            );
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
    public async Task CompleteAsync_OnTooManyRequests_ReturnsRateLimited()
    {
        var sut = BuildProvider(HttpStatusCode.TooManyRequests, "{}");

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.rateLimited"));
    }

    [Test]
    public async Task CompleteAsync_WithEmptyDeploymentId_ReturnsInvalidRequest()
    {
        var sut = BuildProvider(HttpStatusCode.OK, "{}", deploymentId: string.Empty);

        var result = await sut.CompleteAsync(BuildPingRequest(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo("llm.invalidRequest"));
    }

    [Test]
    public void ProviderName_ReturnsAzureOpenAI()
    {
        var sut = BuildProvider(HttpStatusCode.OK, "{}");

        Assert.That(sut.ProviderName, Is.EqualTo(LlmOptions.AzureOpenAiProviderName));
    }

    // -------- helpers --------

    private static LlmRequest BuildPingRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "ping")], Model: null, Temperature: null, MaxTokens: 8);

    private static string BuildHelloBody() =>
        "{ \"model\": \"gpt-4\", "
        + "\"choices\": [{ \"message\": { \"role\": \"assistant\", "
        + "\"content\": \"ok\" } }] }";

    private static AzureOpenAiLlmProvider BuildProvider(
        HttpStatusCode status,
        string responseBody,
        string apiKey = TestApiKey,
        string deploymentId = TestDeploymentId,
        string apiVersion = ""
    )
    {
        var (sut, _, _) = BuildProviderWithHandler(status, responseBody, apiKey, deploymentId, apiVersion);
        return sut;
    }

    private static (AzureOpenAiLlmProvider Provider, Mock<IHttpClientFactory> Factory, Mock<HttpMessageHandler> Handler)
        BuildProviderWithHandler(
            HttpStatusCode status,
            string responseBody,
            string apiKey = TestApiKey,
            string deploymentId = TestDeploymentId,
            string apiVersion = ""
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

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri(TestEndpoint),
        };
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(AzureOpenAiLlmProvider.HttpClientName))
            .Returns(httpClient);

        var monitor = new Mock<IOptionsMonitor<LlmOptions>>();
        monitor
            .Setup(m => m.CurrentValue)
            .Returns(
                new LlmOptions
                {
                    ActiveProvider = LlmOptions.AzureOpenAiProviderName,
                    Providers = new LlmProvidersOptions
                    {
                        AzureOpenAI = new LlmAzureOpenAiProviderOptions
                        {
                            ApiKey = apiKey,
                            Endpoint = TestEndpoint,
                            DeploymentId = deploymentId,
                            ApiVersion = apiVersion,
                        },
                    },
                }
            );

        return (new AzureOpenAiLlmProvider(factory.Object, monitor.Object), factory, handler);
    }
}
