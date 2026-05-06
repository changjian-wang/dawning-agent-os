using System.Net;
using System.Net.Http.Json;
using Dawning.AgentOS.Api.Tests.Helpers;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Api.Tests.Endpoints.Inbox;

/// <summary>
/// In-memory integration tests for
/// <c>POST /api/inbox/items/{id:guid}/summarize</c>. Per ADR-030 the
/// endpoint:
/// <list type="bullet">
///   <item>
///     <description>returns 200 with the summary projection when the
///     active <c>ILlmProvider</c> succeeds;</description>
///   </item>
///   <item>
///     <description>returns 404 with code <c>inbox.notFound</c> when the
///     id does not correspond to a captured item;</description>
///   </item>
///   <item>
///     <description>maps the ADR-028 §H1 LLM error codes to their
///     respective HTTP statuses (401 / 429 / 502 / 400);</description>
///   </item>
///   <item>
///     <description>requires the startup token like every other inbox
///     endpoint (ADR-026 §J2).</description>
///   </item>
/// </list>
/// Tests boot the host with a mock <see cref="ILlmProvider"/> so they
/// do not depend on real LLM credentials; the inbox row is captured
/// through the real <c>POST /api/inbox</c> path so the round-trip
/// goes through <see cref="Persistence.Inbox.InboxRepository.GetByIdAsync"/>.
/// </summary>
[TestFixture]
public sealed class InboxSummarizeEndpointTests
{
    private DawningAgentOsApiFactory _baseFactory = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private Mock<ILlmProvider> _llm = null!;

    [SetUp]
    public void SetUp()
    {
        _baseFactory = new DawningAgentOsApiFactory();
        _llm = new Mock<ILlmProvider>();
        _llm.SetupGet(p => p.ProviderName).Returns("OpenAI");

        // Layer on a service override that swaps the production
        // ILlmProvider for the per-test Moq mock so the integration
        // test does not depend on real LLM credentials.
        _factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILlmProvider>();
                services.AddSingleton(_llm.Object);
            });
        });
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    [Test]
    public async Task Summarize_Returns200WithSummary_WhenLlmSucceeds()
    {
        _llm
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LlmCompletion>.Success(
                    new LlmCompletion(
                        Content: "用户分享一篇文章。",
                        Model: "gpt-4.1-test",
                        PromptTokens: 12,
                        CompletionTokens: 6,
                        Latency: TimeSpan.FromMilliseconds(250)
                    )
                )
            );

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        var capturedId = await CaptureItemAsync(client, "用户分享了一篇文章");

        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{capturedId}/summarize", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<SummaryPayload>();
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.ItemId, Is.EqualTo(capturedId));
            Assert.That(payload.Summary, Is.EqualTo("用户分享一篇文章。"));
            Assert.That(payload.Model, Is.EqualTo("gpt-4.1-test"));
            Assert.That(payload.PromptTokens, Is.EqualTo(12));
            Assert.That(payload.CompletionTokens, Is.EqualTo(6));
            Assert.That(payload.DurationMs, Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public async Task Summarize_Returns404_WhenInboxItemMissing()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        var unknownId = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{unknownId}/summarize", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Code, Is.EqualTo("inbox.notFound"));
    }

    [Test]
    public async Task Summarize_Returns502_WhenLlmUpstreamFails()
    {
        _llm
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LlmCompletion>.Failure(
                    LlmErrors.UpstreamUnavailable("upstream 503")
                )
            );

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        var capturedId = await CaptureItemAsync(client, "anything");

        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{capturedId}/summarize", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.That(problem!.Code, Is.EqualTo("llm.upstreamUnavailable"));
    }

    [Test]
    public async Task Summarize_Returns401_WhenStartupTokenMissing()
    {
        using var client = _factory.CreateClient();

        var someId = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{someId}/summarize", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static async Task<Guid> CaptureItemAsync(HttpClient client, string content)
    {
        var capture = await client.PostAsJsonAsync(
            new Uri("/api/inbox", UriKind.Relative),
            new { content, source = "test" }
        );
        capture.EnsureSuccessStatusCode();
        var snapshot = await capture.Content.ReadFromJsonAsync<CapturePayload>();
        Assert.That(snapshot, Is.Not.Null);
        return snapshot!.Id;
    }

    private sealed record CapturePayload(Guid Id);

    private sealed record SummaryPayload(
        Guid ItemId,
        string Summary,
        string Model,
        int? PromptTokens,
        int? CompletionTokens,
        long DurationMs
    );

    private sealed record ProblemPayload(string Code);
}
