using System.Net;
using System.Net.Http.Json;
using Dawning.AgentOS.Api.Tests.Helpers;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Llm;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Api.Tests.Endpoints.Chat;

/// <summary>
/// In-memory integration tests for the chat endpoints registered by
/// <c>ChatEndpoints.MapChatEndpoints</c> per ADR-032 §决策 G1.
/// V0 surfaces:
/// <list type="number">
///   <item><description><c>POST /api/chat/sessions</c> — create empty session.</description></item>
///   <item><description><c>GET  /api/chat/sessions</c> — paged list ordered by <c>updated_at DESC</c>.</description></item>
///   <item><description><c>GET  /api/chat/sessions/{id}/messages</c> — full history; 404 on missing id.</description></item>
///   <item><description><c>POST /api/chat/sessions/{id}/messages</c> — SSE stream of <c>chunk</c>/<c>done</c>/<c>error</c>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Tests boot the host with a mock <see cref="ILlmProvider"/> so they
/// never call out to a real upstream; the <c>CompleteStreamAsync</c>
/// mock returns hand-rolled <see cref="LlmStreamChunk"/> sequences so
/// the SSE writer can be probed end-to-end. A separate test confirms
/// the streaming endpoint's pre-stream validation surfaces as a 400
/// JSON ProblemDetails (the headers cannot have shipped yet).
/// </remarks>
[TestFixture]
public sealed class ChatEndpointsTests
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
    public async Task PostSessions_Returns200WithNewSession()
    {
        using var client = CreateAuthorizedClient();

        var response = await client.PostAsync(
            new Uri("/api/chat/sessions", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<CreateSessionPayload>();
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.Session, Is.Not.Null);
            Assert.That(payload.Session.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                payload.Session.Id.Version,
                Is.EqualTo(7),
                "ADR-032 §决策 B1: session id must be a UUIDv7"
            );
            Assert.That(
                payload.Session.Title,
                Is.EqualTo("新会话"),
                "ADR-032 §决策 D1: placeholder title until first user turn"
            );
            Assert.That(
                payload.Session.CreatedAt,
                Is.EqualTo(DawningAgentOsApiFactory.NowUtc),
                "AppService stamps timestamps from the injected clock"
            );
        });
    }

    [Test]
    public async Task PostSessions_Returns401_WhenStartupTokenIsMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(
            new Uri("/api/chat/sessions", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetSessions_ReturnsCreatedSessionsOrderedByUpdatedAtDesc()
    {
        using var client = CreateAuthorizedClient();

        // Create two sessions back to back; both share the fixed clock
        // value so ordering is by insertion only — but the second one
        // is enough to confirm the round-trip lands.
        var first = await CreateSessionAsync(client);
        var second = await CreateSessionAsync(client);

        var response = await client.GetAsync(new Uri("/api/chat/sessions", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var page = await response.Content.ReadFromJsonAsync<SessionListPayload>();
        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.Items, Is.Not.Null);
            Assert.That(page.Items, Has.Length.EqualTo(2));
            Assert.That(
                page.Items.Select(i => i.Id),
                Is.EquivalentTo(new[] { first, second }),
                "both created sessions must appear in the list"
            );
            Assert.That(
                page.Limit,
                Is.EqualTo(50),
                "default limit is 50 per ChatEndpoints.DefaultListLimit"
            );
            Assert.That(page.Offset, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetSessions_Returns400_WhenLimitOutOfRange()
    {
        using var client = CreateAuthorizedClient();

        var response = await client.GetAsync(
            new Uri("/api/chat/sessions?limit=0", UriKind.Relative)
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Code, Is.EqualTo("chat.limit.outOfRange"));
    }

    [Test]
    public async Task GetMessages_Returns404_WhenSessionMissing()
    {
        using var client = CreateAuthorizedClient();
        var unknownId = Guid.CreateVersion7(DateTimeOffset.UtcNow);

        var response = await client.GetAsync(
            new Uri($"/api/chat/sessions/{unknownId}/messages", UriKind.Relative)
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Code, Is.EqualTo("chat.sessionNotFound"));
    }

    [Test]
    public async Task GetMessages_ReturnsEmptyList_ForFreshSession()
    {
        using var client = CreateAuthorizedClient();
        var sessionId = await CreateSessionAsync(client);

        var response = await client.GetAsync(
            new Uri($"/api/chat/sessions/{sessionId}/messages", UriKind.Relative)
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var messages = await response.Content.ReadFromJsonAsync<MessagePayload[]>();
        Assert.That(messages, Is.Not.Null);
        Assert.That(messages, Is.Empty);
    }

    [Test]
    public async Task PostMessage_StreamsSseFrames_AndPersistsAssistant()
    {
        _llm.Setup(p =>
                p.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                BuildStream(
                    LlmStreamChunk.ForDelta("你好"),
                    LlmStreamChunk.ForDelta("，主人。"),
                    LlmStreamChunk.ForDone(
                        model: "gpt-4.1-test",
                        promptTokens: 42,
                        completionTokens: 5,
                        latency: TimeSpan.FromMilliseconds(123)
                    )
                )
            );

        using var client = CreateAuthorizedClient();
        var sessionId = await CreateSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/chat/sessions/{sessionId}/messages", UriKind.Relative),
            new { content = "你是谁？" }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("text/event-stream")
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("event: chunk\ndata: {\"delta\":\"你好\"}"));
            Assert.That(body, Does.Contain("event: chunk\ndata: {\"delta\":\"，主人。\"}"));
            Assert.That(body, Does.Contain("event: done"));
            Assert.That(body, Does.Contain("\"model\":\"gpt-4.1-test\""));
            Assert.That(body, Does.Contain("\"promptTokens\":42"));
            Assert.That(body, Does.Contain("\"completionTokens\":5"));
            Assert.That(body, Does.Contain("\"durationMs\":123"));
        });

        // After the stream ends both the user and assistant turns must
        // be persisted so a subsequent GET /messages returns them in
        // send order — the assistant's content equals the joined deltas.
        var messages = await client.GetFromJsonAsync<MessagePayload[]>(
            new Uri($"/api/chat/sessions/{sessionId}/messages", UriKind.Relative)
        );
        Assert.That(messages, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Length.EqualTo(2));
            Assert.That(messages![0].Role, Is.EqualTo("user"));
            Assert.That(messages[0].Content, Is.EqualTo("你是谁？"));
            Assert.That(messages[1].Role, Is.EqualTo("assistant"));
            Assert.That(messages[1].Content, Is.EqualTo("你好，主人。"));
            Assert.That(messages[1].Model, Is.EqualTo("gpt-4.1-test"));
            Assert.That(messages[1].PromptTokens, Is.EqualTo(42));
            Assert.That(messages[1].CompletionTokens, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task PostMessage_EmitsErrorFrame_AndDoesNotPersistAssistant_OnLlmError()
    {
        var domainError = LlmErrors.UpstreamUnavailable("503 from upstream");
        _llm.Setup(p =>
                p.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                BuildStream(LlmStreamChunk.ForDelta("半句话"), LlmStreamChunk.ForError(domainError))
            );

        using var client = CreateAuthorizedClient();
        var sessionId = await CreateSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/chat/sessions/{sessionId}/messages", UriKind.Relative),
            new { content = "出错试试" }
        );

        // Mid-stream errors do NOT short-circuit to a 4xx ProblemDetails;
        // headers have already shipped (status 200, content-type
        // text/event-stream) and the failure is reported as an SSE
        // error frame. ADR-032 §决策 H1.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("text/event-stream")
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("event: chunk\ndata: {\"delta\":\"半句话\"}"));
            Assert.That(body, Does.Contain("event: error"));
            Assert.That(body, Does.Contain("\"code\":\"llm.upstreamUnavailable\""));
        });

        // ADR-032 §决策 J1: user turn persisted before LLM call, assistant
        // SKIPPED on stream-error. So GET /messages returns one row.
        var messages = await client.GetFromJsonAsync<MessagePayload[]>(
            new Uri($"/api/chat/sessions/{sessionId}/messages", UriKind.Relative)
        );
        Assert.That(messages, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Length.EqualTo(1));
            Assert.That(messages![0].Role, Is.EqualTo("user"));
            Assert.That(messages[0].Content, Is.EqualTo("出错试试"));
        });
    }

    [Test]
    public async Task PostMessage_Returns400_WhenContentEmpty_BeforeAnySseHeadersShip()
    {
        using var client = CreateAuthorizedClient();
        var sessionId = await CreateSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/chat/sessions/{sessionId}/messages", UriKind.Relative),
            new { content = "   " }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Code, Is.EqualTo("chat.content.required"));
    }

    [Test]
    public async Task PostMessage_Returns404_WhenSessionMissing_BeforeAnySseHeadersShip()
    {
        using var client = CreateAuthorizedClient();
        var unknownId = Guid.CreateVersion7(DateTimeOffset.UtcNow);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/chat/sessions/{unknownId}/messages", UriKind.Relative),
            new { content = "anything" }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Code, Is.EqualTo("chat.sessionNotFound"));
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);
        return client;
    }

    private static async Task<Guid> CreateSessionAsync(HttpClient client)
    {
        var response = await client.PostAsync(
            new Uri("/api/chat/sessions", UriKind.Relative),
            content: null
        );
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CreateSessionPayload>();
        Assert.That(payload, Is.Not.Null);
        return payload!.Session.Id;
    }

#pragma warning disable CS1998 // async iterator without awaits — intentional, we are mocking a stream
    private static async IAsyncEnumerable<LlmStreamChunk> BuildStream(params LlmStreamChunk[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
#pragma warning restore CS1998

    private sealed record CreateSessionPayload(SessionPayload Session);

    private sealed record SessionPayload(
        Guid Id,
        string Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    private sealed record SessionListPayload(SessionPayload[] Items, int Limit, int Offset);

    private sealed record MessagePayload(
        Guid Id,
        Guid SessionId,
        string Role,
        string Content,
        DateTimeOffset CreatedAt,
        string? Model,
        int? PromptTokens,
        int? CompletionTokens
    );

    private sealed record ProblemPayload(string Code);
}
