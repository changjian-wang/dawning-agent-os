using System.Net;
using System.Net.Http.Json;
using Dawning.AgentOS.Api.Tests.Helpers;
using NUnit.Framework;

namespace Dawning.AgentOS.Api.Tests.Endpoints.Runtime;

/// <summary>
/// In-memory integration tests for the runtime-status endpoint. Booted
/// via <see cref="DawningAgentOsApiFactory"/>; verifies (a) the
/// happy-path payload shape, (b) the startup-token middleware rejects
/// missing tokens with 401, (c) the same middleware rejects mismatched
/// tokens with 401, and (d) per ADR-024 §H1 the database probe reports
/// <c>Ready=true</c> with the seed schema version applied.
/// </summary>
[TestFixture]
public sealed class RuntimeEndpointsTests
{
    private DawningAgentOsApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new DawningAgentOsApiFactory();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public async Task GetStatus_ReturnsOkWithRuntimeStatusDto_WhenTokenIsValid()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", DawningAgentOsApiFactory.ExpectedToken);

        var response = await client.GetAsync(new Uri("/api/runtime/status", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<RuntimeStatusPayload>();
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.StartedAtUtc, Is.EqualTo(DawningAgentOsApiFactory.StartedAtUtc));
            Assert.That(
                payload.NowUtc,
                Is.EqualTo(DawningAgentOsApiFactory.NowUtc)
                    .Within(DawningAgentOsApiFactory.MaxClockDrift)
            );
            Assert.That(payload.Healthy, Is.True);
            Assert.That(
                payload.Uptime,
                Is.EqualTo(DawningAgentOsApiFactory.NowUtc - DawningAgentOsApiFactory.StartedAtUtc)
                    .Within(DawningAgentOsApiFactory.MaxClockDrift)
            );
            Assert.That(payload.Database, Is.Not.Null, "database snapshot must be present");
            Assert.That(payload.Database!.Ready, Is.True, "schema bootstrap must have succeeded");
            Assert.That(
                payload.Database.SchemaVersion,
                Is.GreaterThanOrEqualTo(1L),
                "the seed migration must have been applied (ADR-024 §3)"
            );
        });
    }

    [Test]
    public async Task GetStatus_Returns401_WhenTokenIsMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/runtime/status", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
    }

    [Test]
    public async Task GetStatus_Returns401_WhenTokenIsInvalid()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Startup-Token", "wrong-token");

        var response = await client.GetAsync(new Uri("/api/runtime/status", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json")
        );
    }

    /// <summary>
    /// Local mirror of the <c>RuntimeStatus</c> record used purely for
    /// JSON deserialization. Declared with the same property names the
    /// API serializes (camelCase by default).
    /// </summary>
    private sealed record RuntimeStatusPayload(
        DateTimeOffset StartedAtUtc,
        DateTimeOffset NowUtc,
        TimeSpan Uptime,
        bool Healthy,
        DatabaseStatusPayload? Database
    );

    private sealed record DatabaseStatusPayload(bool Ready, long? SchemaVersion, string? FilePath);
}
