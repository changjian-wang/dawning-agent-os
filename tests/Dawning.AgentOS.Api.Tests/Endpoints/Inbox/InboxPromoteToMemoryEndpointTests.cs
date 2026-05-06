using System.Net;
using System.Net.Http.Json;
using Dawning.AgentOS.Api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Dawning.AgentOS.Api.Tests.Endpoints.Inbox;

/// <summary>
/// In-memory integration tests for
/// <c>POST /api/inbox/items/{id:guid}/promote-to-memory</c>. Per ADR-034
/// the endpoint:
/// <list type="bullet">
///   <item>
///     <description>returns 200 with a <c>MemoryEntryDto</c> projection
///     when the inbox item exists; the persisted ledger row carries
///     <c>source = "InboxAction"</c>, <c>scope = "inbox"</c>,
///     <c>isExplicit = true</c>, <c>confidence = 1.0</c> per §决策
///     A1 / B1 / C1;</description>
///   </item>
///   <item>
///     <description>returns 404 with code <c>inbox.notFound</c> when the
///     id does not correspond to a captured item (§决策 G1);</description>
///   </item>
///   <item>
///     <description>does not dedup: repeated promotion of the same
///     inbox item produces multiple distinct ledger rows
///     (§决策 F1);</description>
///   </item>
///   <item>
///     <description>requires the startup token like every other inbox
///     endpoint (ADR-026 §J2).</description>
///   </item>
/// </list>
/// Tests boot the host through <see cref="DawningAgentOsApiFactory"/>
/// so the round-trip exercises the real Dapper / SQLite stack —
/// <c>InboxRepository.GetByIdAsync</c> on the read side and
/// <c>MemoryLedgerRepository.AddAsync</c> on the write side. No LLM
/// override is needed because the promote path is purely backend
/// data-plumbing.
/// </summary>
[TestFixture]
public sealed class InboxPromoteToMemoryEndpointTests
{
    private DawningAgentOsApiFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new DawningAgentOsApiFactory();
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public async Task Promote_Returns200WithLedgerProjection_WhenInboxItemExists()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        var capturedId = await CaptureItemAsync(client, "用户希望被记住的一段话");

        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{capturedId}/promote-to-memory", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(payload.Content, Is.EqualTo("用户希望被记住的一段话"));
            Assert.That(payload.Scope, Is.EqualTo("inbox"));
            Assert.That(payload.Source, Is.EqualTo("InboxAction"));
            Assert.That(payload.IsExplicit, Is.True);
            Assert.That(payload.Confidence, Is.EqualTo(1.0));
            Assert.That(payload.Sensitivity, Is.EqualTo("Normal"));
            Assert.That(payload.Status, Is.EqualTo("Active"));
            Assert.That(payload.DeletedAt, Is.Null);
        });
    }

    [Test]
    public async Task Promote_PersistsRowVisibleViaMemoryListEndpoint()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        var capturedId = await CaptureItemAsync(client, "需要在 Memory 列表里看到的内容");

        var promote = await client.PostAsync(
            new Uri($"/api/inbox/items/{capturedId}/promote-to-memory", UriKind.Relative),
            content: null
        );
        promote.EnsureSuccessStatusCode();
        var promoted = await promote.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(promoted, Is.Not.Null);

        var list = await client.GetAsync(new Uri("/api/memory", UriKind.Relative));
        list.EnsureSuccessStatusCode();
        var listPayload = await list.Content.ReadFromJsonAsync<MemoryListPayload>();
        Assert.That(listPayload, Is.Not.Null);
        var match = listPayload!.Items.SingleOrDefault(i => i.Id == promoted!.Id);
        Assert.That(match, Is.Not.Null, "promoted ledger row should be visible in /api/memory list");
        Assert.That(match!.Source, Is.EqualTo("InboxAction"));
        Assert.That(match.Scope, Is.EqualTo("inbox"));
        Assert.That(match.Content, Is.EqualTo("需要在 Memory 列表里看到的内容"));
    }

    [Test]
    public async Task Promote_Returns404_WhenInboxItemMissing()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        var unknownId = Guid.CreateVersion7(DateTimeOffset.UtcNow);

        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{unknownId}/promote-to-memory", UriKind.Relative),
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
    public async Task Promote_TwiceProducesDistinctLedgerRows_NoDedup()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        var capturedId = await CaptureItemAsync(client, "重复点击 Save 应当产生两条");

        var first = await client.PostAsync(
            new Uri($"/api/inbox/items/{capturedId}/promote-to-memory", UriKind.Relative),
            content: null
        );
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<MemoryEntryPayload>();

        var second = await client.PostAsync(
            new Uri($"/api/inbox/items/{capturedId}/promote-to-memory", UriKind.Relative),
            content: null
        );
        second.EnsureSuccessStatusCode();
        var secondPayload = await second.Content.ReadFromJsonAsync<MemoryEntryPayload>();

        Assert.That(firstPayload, Is.Not.Null);
        Assert.That(secondPayload, Is.Not.Null);
        // ADR-034 §决策 F1 — no dedup; two POSTs ⇒ two distinct rows.
        Assert.That(secondPayload!.Id, Is.Not.EqualTo(firstPayload!.Id));
        Assert.That(secondPayload.Content, Is.EqualTo(firstPayload.Content));
    }

    [Test]
    public async Task Promote_Returns401_WhenStartupTokenMissing()
    {
        using var client = _factory.CreateClient();

        var someId = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{someId}/promote-to-memory", UriKind.Relative),
            content: null
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Promote_PassesContentVerbatim_NoTrim()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Startup-Token",
            DawningAgentOsApiFactory.ExpectedToken
        );

        const string content = "  原文带前后空格 — 不能被服务端动手改  ";
        var capturedId = await CaptureItemAsync(client, content);

        var response = await client.PostAsync(
            new Uri($"/api/inbox/items/{capturedId}/promote-to-memory", UriKind.Relative),
            content: null
        );

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(payload, Is.Not.Null);
        // ADR-034 §决策 B1 — coordinator copies InboxItem.Content verbatim;
        // the leading/trailing spaces survive the round-trip.
        Assert.That(payload!.Content, Is.EqualTo(content));
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

    private sealed record MemoryEntryPayload(
        Guid Id,
        string Content,
        string Scope,
        string Source,
        bool IsExplicit,
        double Confidence,
        string Sensitivity,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? DeletedAt
    );

    private sealed record MemoryListPayload(IReadOnlyList<MemoryEntryPayload> Items, long Total);

    private sealed record ProblemPayload(string Code);
}
