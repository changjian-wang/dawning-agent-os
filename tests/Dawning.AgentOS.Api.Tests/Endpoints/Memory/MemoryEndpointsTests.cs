using System.Net;
using System.Net.Http.Json;
using Dawning.AgentOS.Api.Tests.Helpers;
using NUnit.Framework;

namespace Dawning.AgentOS.Api.Tests.Endpoints.Memory;

/// <summary>
/// In-memory integration tests for the memory ledger endpoints. Per
/// ADR-033 §决策 J1 V0 surfaces five endpoints under <c>/api/memory</c>;
/// per ADR-033 §决策 J1 all reuse the startup-token middleware (no
/// per-user identity in V0). These tests verify (a) the happy paths
/// for every verb, (b) startup-token enforcement on at least one
/// route, (c) request-body validation surfacing as HTTP 400, (d)
/// the bespoke <c>memory.notFound</c> → 404 and
/// <c>memory.invalidStatusTransition</c> → 422 mappings, and (e)
/// create → list / get / patch / delete round-trips with the UUIDv7
/// identifier preserved.
/// </summary>
[TestFixture]
public sealed class MemoryEndpointsTests
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

    // ===============================================================
    // POST /api/memory
    // ===============================================================

    [Test]
    public async Task PostMemory_Returns200WithDto_WhenRequestIsValid()
    {
        using var client = AuthorizedClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/memory", UriKind.Relative),
            new { content = "favorite tea: longjing" }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                dto.Id.Version,
                Is.EqualTo(7),
                "ADR-033 §决策 J1: identifier must be a UUIDv7"
            );
            Assert.That(dto.Content, Is.EqualTo("favorite tea: longjing"));
            Assert.That(dto.Scope, Is.EqualTo("global"));
            Assert.That(
                dto.Source,
                Is.EqualTo("UserExplicit"),
                "ADR-033 §决策 B1: V0 only writes UserExplicit entries"
            );
            Assert.That(dto.IsExplicit, Is.True);
            Assert.That(dto.Confidence, Is.EqualTo(1.0));
            Assert.That(dto.Status, Is.EqualTo("Active"));
            Assert.That(
                dto.CreatedAt,
                Is.EqualTo(DawningAgentOsApiFactory.NowUtc)
                    .Within(DawningAgentOsApiFactory.MaxClockDrift),
                "AppService stamps createdAt from the injected clock"
            );
            Assert.That(dto.DeletedAt, Is.Null);
        });
    }

    [Test]
    public async Task PostMemory_Returns401_WhenStartupTokenMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/memory", UriKind.Relative),
            new { content = "x" }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostMemory_Returns400_WhenContentIsBlank()
    {
        using var client = AuthorizedClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/memory", UriKind.Relative),
            new { content = "   " }
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
    }

    // ===============================================================
    // GET /api/memory
    // ===============================================================

    [Test]
    public async Task GetMemory_Returns200WithEmptyPage_WhenStoreIsEmpty()
    {
        using var client = AuthorizedClient();

        var response = await client.GetAsync(new Uri("/api/memory", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var page = await response.Content.ReadFromJsonAsync<MemoryListPayload>();
        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.Total, Is.EqualTo(0L));
            Assert.That(page.Items, Is.Not.Null);
            Assert.That(page.Items, Is.Empty);
            Assert.That(
                page.Limit,
                Is.EqualTo(50),
                "default limit is 50 per ADR-033 §决策 J1 / MemoryEndpoints.DefaultListLimit"
            );
            Assert.That(page.Offset, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetMemory_ReflectsPriorPostedEntry_InTotalAndFirstRow()
    {
        using var client = AuthorizedClient();

        var captured = await CreateEntryAsync(client, "round-trip");

        var listResponse = await client.GetAsync(
            new Uri("/api/memory?limit=10&offset=0", UriKind.Relative)
        );
        listResponse.EnsureSuccessStatusCode();
        var page = await listResponse.Content.ReadFromJsonAsync<MemoryListPayload>();

        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.Total, Is.EqualTo(1L));
            Assert.That(page.Limit, Is.EqualTo(10));
            Assert.That(page.Offset, Is.EqualTo(0));
            Assert.That(page.Items, Has.Count.EqualTo(1));
            Assert.That(page.Items[0].Id, Is.EqualTo(captured.Id));
            Assert.That(page.Items[0].Content, Is.EqualTo("round-trip"));
        });
    }

    [Test]
    public async Task GetMemory_Returns400_WhenLimitOutOfRange()
    {
        using var client = AuthorizedClient();

        var response = await client.GetAsync(new Uri("/api/memory?limit=0", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetMemory_Returns401_WhenStartupTokenMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/memory", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetMemory_DefaultExcludesSoftDeleted_AndIncludeFlagSurfacesThem()
    {
        using var client = AuthorizedClient();
        var live = await CreateEntryAsync(client, "live");
        var dead = await CreateEntryAsync(client, "dead");

        var deleteResponse = await client.DeleteAsync(
            new Uri($"/api/memory/{dead.Id}", UriKind.Relative)
        );
        deleteResponse.EnsureSuccessStatusCode();

        var defaultPage = (await GetPageAsync(client, "/api/memory"))!;
        var includePage = (await GetPageAsync(client, "/api/memory?includeSoftDeleted=true"))!;

        Assert.Multiple(() =>
        {
            Assert.That(defaultPage.Total, Is.EqualTo(1L));
            Assert.That(defaultPage.Items, Has.Count.EqualTo(1));
            Assert.That(defaultPage.Items[0].Id, Is.EqualTo(live.Id));
            Assert.That(includePage.Total, Is.EqualTo(2L));
            Assert.That(includePage.Items, Has.Count.EqualTo(2));
        });
    }

    // ===============================================================
    // GET /api/memory/{id}
    // ===============================================================

    [Test]
    public async Task GetMemoryById_Returns200WithDto_WhenEntryExists()
    {
        using var client = AuthorizedClient();
        var captured = await CreateEntryAsync(client, "fetch-me");

        var response = await client.GetAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative)
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Id, Is.EqualTo(captured.Id));
        Assert.That(dto.Content, Is.EqualTo("fetch-me"));
    }

    [Test]
    public async Task GetMemoryById_Returns404_WhenEntryMissing()
    {
        using var client = AuthorizedClient();

        var response = await client.GetAsync(
            new Uri($"/api/memory/{Guid.NewGuid()}", UriKind.Relative)
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
    }

    // ===============================================================
    // PATCH /api/memory/{id}
    // ===============================================================

    [Test]
    public async Task PatchMemory_Returns200WithDto_WhenContentChanges()
    {
        using var client = AuthorizedClient();
        var captured = await CreateEntryAsync(client, "before");

        var patch = await client.PatchAsJsonAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative),
            new { content = "after", scope = "project:dawning" }
        );

        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await patch.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.Content, Is.EqualTo("after"));
            Assert.That(dto.Scope, Is.EqualTo("project:dawning"));
            Assert.That(dto.Status, Is.EqualTo("Active"));
        });
    }

    [Test]
    public async Task PatchMemory_Returns404_WhenEntryMissing()
    {
        using var client = AuthorizedClient();

        var patch = await client.PatchAsJsonAsync(
            new Uri($"/api/memory/{Guid.NewGuid()}", UriKind.Relative),
            new { content = "ghost" }
        );

        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PatchMemory_Returns400_WhenBodyEmpty()
    {
        using var client = AuthorizedClient();
        var captured = await CreateEntryAsync(client, "x");

        var patch = await client.PatchAsJsonAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative),
            new { }
        );

        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PatchMemory_Returns422_WhenStatusTransitionIllegal()
    {
        using var client = AuthorizedClient();
        var captured = await CreateEntryAsync(client, "x");

        // Soft-delete first, then attempt to drive it to Archived (illegal:
        // SoftDeleted only allows Restore → Active per ADR-033 §决策 D1).
        var del = await client.DeleteAsync(new Uri($"/api/memory/{captured.Id}", UriKind.Relative));
        del.EnsureSuccessStatusCode();

        var patch = await client.PatchAsJsonAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative),
            new { status = "Archived" }
        );

        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task PatchMemory_Returns200_WhenStatusActiveRestoresSoftDeletedEntry()
    {
        using var client = AuthorizedClient();
        var captured = await CreateEntryAsync(client, "restore-me");
        await client.DeleteAsync(new Uri($"/api/memory/{captured.Id}", UriKind.Relative));

        var patch = await client.PatchAsJsonAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative),
            new { status = "Active" }
        );

        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await patch.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.Status, Is.EqualTo("Active"));
            Assert.That(dto.DeletedAt, Is.Null);
        });
    }

    // ===============================================================
    // DELETE /api/memory/{id}
    // ===============================================================

    [Test]
    public async Task DeleteMemory_Returns200WithDto_WhenEntryExists()
    {
        using var client = AuthorizedClient();
        var captured = await CreateEntryAsync(client, "x");

        var response = await client.DeleteAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative)
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.Status, Is.EqualTo("SoftDeleted"));
            Assert.That(dto.DeletedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task DeleteMemory_Returns404_WhenEntryMissing()
    {
        using var client = AuthorizedClient();

        var response = await client.DeleteAsync(
            new Uri($"/api/memory/{Guid.NewGuid()}", UriKind.Relative)
        );

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteMemory_Returns422_WhenAlreadySoftDeleted()
    {
        using var client = AuthorizedClient();
        var captured = await CreateEntryAsync(client, "x");
        var first = await client.DeleteAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative)
        );
        first.EnsureSuccessStatusCode();

        var second = await client.DeleteAsync(
            new Uri($"/api/memory/{captured.Id}", UriKind.Relative)
        );

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    // ===============================================================
    // helpers
    // ===============================================================

    private HttpClient AuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);
        return client;
    }

    private static async Task<MemoryEntryPayload> CreateEntryAsync(
        HttpClient client,
        string content
    )
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/memory", UriKind.Relative),
            new { content }
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<MemoryEntryPayload>();
        Assert.That(dto, Is.Not.Null);
        return dto!;
    }

    private static async Task<MemoryListPayload?> GetPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemoryListPayload>();
    }

    /// <summary>Local mirror of <c>MemoryEntryDto</c> for JSON deserialization.</summary>
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

    /// <summary>Local mirror of <c>MemoryEntryListPage</c> for JSON deserialization.</summary>
    private sealed record MemoryListPayload(
        IReadOnlyList<MemoryEntryPayload> Items,
        long Total,
        int Limit,
        int Offset
    );
}
