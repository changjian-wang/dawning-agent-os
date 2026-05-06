using System.Net;
using System.Net.Http.Json;
using Dawning.AgentOS.Api.Tests.Helpers;
using NUnit.Framework;

namespace Dawning.AgentOS.Api.Tests.Endpoints.Inbox;

/// <summary>
/// In-memory integration tests for the inbox endpoints. Per ADR-026 §7
/// V0 surfaces <c>POST /api/inbox</c> and <c>GET /api/inbox</c>; per
/// ADR-026 §J2 both reuse the startup-token middleware (no per-user
/// identity in V0). These tests verify (a) the happy paths for both
/// verbs, (b) startup-token enforcement, (c) request-body validation
/// surfacing as HTTP 400, and (d) capture → list round-trip with
/// the UUIDv7 identifier preserved.
/// </summary>
[TestFixture]
public sealed class InboxEndpointsTests
{
    private DawningAgentOsApiFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        // Each test gets a fresh factory so the in-memory SQLite store
        // resets between cases — list-after-capture round-trips don't
        // need to defend against bleed-through from earlier tests.
        _factory = new DawningAgentOsApiFactory();
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public async Task PostInbox_Returns200WithSnapshot_WhenRequestIsValid()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/inbox", UriKind.Relative),
            new { content = "hello inbox", source = "chat" }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var snapshot = await response.Content.ReadFromJsonAsync<InboxItemPayload>();
        Assert.That(snapshot, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(snapshot!.Content, Is.EqualTo("hello inbox"));
            Assert.That(snapshot.Source, Is.EqualTo("chat"));
            Assert.That(snapshot.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                snapshot.Id.Version,
                Is.EqualTo(7),
                "ADR-026 §B2': identifier must be a UUIDv7"
            );
            Assert.That(
                snapshot.CapturedAtUtc,
                Is.EqualTo(DawningAgentOsApiFactory.NowUtc),
                "AppService stamps capturedAtUtc from the injected clock"
            );
        });
    }

    [Test]
    public async Task PostInbox_Returns401_WhenStartupTokenIsMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/inbox", UriKind.Relative),
            new { content = "x" }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostInbox_Returns400_WhenContentIsEmpty()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/inbox", UriKind.Relative),
            new { content = "  ", source = (string?)null }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
    }

    [Test]
    public async Task GetInbox_Returns200WithEmptyPage_WhenStoreIsEmpty()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);

        var response = await client.GetAsync(new Uri("/api/inbox", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var page = await response.Content.ReadFromJsonAsync<InboxListPayload>();
        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.Total, Is.EqualTo(0L));
            Assert.That(page.Items, Is.Not.Null);
            Assert.That(page.Items, Is.Empty);
            Assert.That(
                page.Limit,
                Is.EqualTo(50),
                "default limit is 50 per ADR-026 §C2 / InboxEndpoints.DefaultListLimit"
            );
            Assert.That(page.Offset, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetInbox_ReflectsPriorPostedItem_InTotalAndFirstRow()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);

        var capture = await client.PostAsJsonAsync(
            new Uri("/api/inbox", UriKind.Relative),
            new { content = "round-trip", source = "smoke" }
        );
        capture.EnsureSuccessStatusCode();
        var captured = await capture.Content.ReadFromJsonAsync<InboxItemPayload>();
        Assert.That(captured, Is.Not.Null);

        var list = await client.GetAsync(new Uri("/api/inbox?limit=10&offset=0", UriKind.Relative));
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<InboxListPayload>();
        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.Total, Is.EqualTo(1L));
            Assert.That(page.Limit, Is.EqualTo(10));
            Assert.That(page.Offset, Is.EqualTo(0));
            Assert.That(page.Items, Has.Count.EqualTo(1));
            Assert.That(page.Items[0].Id, Is.EqualTo(captured!.Id));
            Assert.That(page.Items[0].Content, Is.EqualTo("round-trip"));
        });
    }

    [Test]
    public async Task GetInbox_Returns400_WhenLimitIsOutOfRange()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);

        var response = await client.GetAsync(new Uri("/api/inbox?limit=0", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetInbox_Returns401_WhenStartupTokenIsMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/inbox", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// Local mirror of <c>InboxItemSnapshot</c> for JSON deserialization.
    /// </summary>
    private sealed record InboxItemPayload(
        Guid Id,
        string Content,
        string? Source,
        DateTimeOffset CapturedAtUtc,
        DateTimeOffset CreatedAt
    );

    /// <summary>
    /// Local mirror of <c>InboxListPage</c> for JSON deserialization.
    /// </summary>
    private sealed record InboxListPayload(
        IReadOnlyList<InboxItemPayload> Items,
        long Total,
        int Limit,
        int Offset
    );
}
