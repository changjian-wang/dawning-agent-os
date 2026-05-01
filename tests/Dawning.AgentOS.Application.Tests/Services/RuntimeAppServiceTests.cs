using System.Data.Common;
using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Abstractions.Hosting;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Services;
using Microsoft.Data.Sqlite;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RuntimeAppService"/>. Per ADR-022 the AppService
/// is the canonical Application layer entry point; per ADR-024 §H1 the
/// service also probes the V0 SQLite store and surfaces a
/// <c>DatabaseStatus</c> snapshot. These tests cover the happy path,
/// clock-skew clamping, the database probe's success / failure / empty-table
/// branches, and constructor null-guards.
/// </summary>
/// <remarks>
/// The Application test project may reference Microsoft.Data.Sqlite —
/// LayeringTests asserts on the <c>Dawning.AgentOS.Application</c>
/// production assembly, not on its tests. In-memory SQLite is the
/// quickest way to exercise the probe code path with real
/// <see cref="DbConnection"/> + <see cref="DbCommand"/> instances; mocking
/// the ADO.NET surface is brittle and offers no coverage gain.
/// </remarks>
[TestFixture]
public class RuntimeAppServiceTests
{
    private const string TestDatabasePath = "/tmp/dawning-agent-os-test/agentos.db";

    [Test]
    public async Task GetStatusAsync_ReturnsHealthySnapshotWithComputedUptime()
    {
        var startedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddMinutes(7);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(now);

        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        startProvider.SetupGet(s => s.StartedAtUtc).Returns(startedAt);

        var connectionFactory = CreateInMemorySqliteFactory(out var connection);
        await using (connection)
        {
            await SeedSchemaVersionAsync(connection, version: 1);

            var pathProvider = CreatePathProvider();

            IRuntimeAppService sut = new RuntimeAppService(
                clock.Object,
                startProvider.Object,
                connectionFactory.Object,
                pathProvider.Object
            );

            var result = await sut.GetStatusAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var snapshot = result.Value;
            Assert.That(snapshot.StartedAtUtc, Is.EqualTo(startedAt));
            Assert.That(snapshot.NowUtc, Is.EqualTo(now));
            Assert.That(snapshot.Uptime, Is.EqualTo(TimeSpan.FromMinutes(7)));
            Assert.That(snapshot.Healthy, Is.True);
            Assert.That(snapshot.Database.Ready, Is.True);
            Assert.That(snapshot.Database.SchemaVersion, Is.EqualTo(1));
            Assert.That(snapshot.Database.FilePath, Is.EqualTo(TestDatabasePath));
        }
    }

    [Test]
    public async Task GetStatusAsync_ClampsUptimeToZeroWhenClockSkewMakesNowPrecedeStart()
    {
        var startedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        // Simulate an OS clock that jumped backwards by 30 seconds.
        var now = startedAt.AddSeconds(-30);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(now);

        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        startProvider.SetupGet(s => s.StartedAtUtc).Returns(startedAt);

        var connectionFactory = CreateInMemorySqliteFactory(out var connection);
        await using (connection)
        {
            await SeedSchemaVersionAsync(connection, version: 1);

            var pathProvider = CreatePathProvider();

            var sut = new RuntimeAppService(
                clock.Object,
                startProvider.Object,
                connectionFactory.Object,
                pathProvider.Object
            );

            var result = await sut.GetStatusAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Uptime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(result.Value.NowUtc, Is.EqualTo(now));
        }
    }

    [Test]
    public async Task GetStatusAsync_ReturnsDatabaseNotReady_WhenConnectionFactoryThrows()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        startProvider.SetupGet(s => s.StartedAtUtc).Returns(DateTimeOffset.UtcNow.AddMinutes(-1));

        var connectionFactory = new Mock<IDbConnectionFactory>();
        connectionFactory
            .Setup(f => f.OpenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated SQLite failure"));

        var pathProvider = CreatePathProvider();

        var sut = new RuntimeAppService(
            clock.Object,
            startProvider.Object,
            connectionFactory.Object,
            pathProvider.Object
        );

        var result = await sut.GetStatusAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True, "the endpoint must always succeed (ADR-024 §H1)");
        Assert.That(result.Value.Database.Ready, Is.False);
        Assert.That(result.Value.Database.SchemaVersion, Is.Null);
        Assert.That(
            result.Value.Database.FilePath,
            Is.EqualTo(TestDatabasePath),
            "FilePath is reported even when the connection probe fails"
        );
    }

    [Test]
    public async Task GetStatusAsync_ReturnsDatabaseNotReadyWithNullPath_WhenPathProviderThrows()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        startProvider.SetupGet(s => s.StartedAtUtc).Returns(DateTimeOffset.UtcNow.AddMinutes(-1));

        var connectionFactory = new Mock<IDbConnectionFactory>();

        var pathProvider = new Mock<IAppDataPathProvider>();
        pathProvider
            .Setup(p => p.GetDatabasePath())
            .Throws(new IOException("simulated read-only home"));

        var sut = new RuntimeAppService(
            clock.Object,
            startProvider.Object,
            connectionFactory.Object,
            pathProvider.Object
        );

        var result = await sut.GetStatusAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Database.Ready, Is.False);
        Assert.That(result.Value.Database.SchemaVersion, Is.Null);
        Assert.That(result.Value.Database.FilePath, Is.Null);
        connectionFactory.Verify(f => f.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetStatusAsync_ReturnsNullSchemaVersion_WhenSchemaTableIsEmpty()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        startProvider.SetupGet(s => s.StartedAtUtc).Returns(DateTimeOffset.UtcNow.AddMinutes(-1));

        var connectionFactory = CreateInMemorySqliteFactory(out var connection);
        await using (connection)
        {
            // Create the table but do NOT insert any rows.
            await using var ddl = connection.CreateCommand();
            ddl.CommandText =
                "CREATE TABLE __schema_version (version INTEGER NOT NULL PRIMARY KEY, applied_at TEXT NOT NULL);";
            await ddl.ExecuteNonQueryAsync();

            var pathProvider = CreatePathProvider();

            var sut = new RuntimeAppService(
                clock.Object,
                startProvider.Object,
                connectionFactory.Object,
                pathProvider.Object
            );

            var result = await sut.GetStatusAsync(CancellationToken.None);

            Assert.That(result.Value.Database.Ready, Is.True);
            Assert.That(result.Value.Database.SchemaVersion, Is.Null);
        }
    }

    [Test]
    public void Constructor_ThrowsWhenClockIsNull()
    {
        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        var connectionFactory = new Mock<IDbConnectionFactory>();
        var pathProvider = new Mock<IAppDataPathProvider>();

        Assert.That(
            () =>
                new RuntimeAppService(
                    null!,
                    startProvider.Object,
                    connectionFactory.Object,
                    pathProvider.Object
                ),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("clock")
        );
    }

    [Test]
    public void Constructor_ThrowsWhenStartTimeProviderIsNull()
    {
        var clock = new Mock<IClock>();
        var connectionFactory = new Mock<IDbConnectionFactory>();
        var pathProvider = new Mock<IAppDataPathProvider>();

        Assert.That(
            () =>
                new RuntimeAppService(
                    clock.Object,
                    null!,
                    connectionFactory.Object,
                    pathProvider.Object
                ),
            Throws
                .TypeOf<ArgumentNullException>()
                .With.Property("ParamName")
                .EqualTo("startTimeProvider")
        );
    }

    [Test]
    public void Constructor_ThrowsWhenConnectionFactoryIsNull()
    {
        var clock = new Mock<IClock>();
        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        var pathProvider = new Mock<IAppDataPathProvider>();

        Assert.That(
            () =>
                new RuntimeAppService(
                    clock.Object,
                    startProvider.Object,
                    null!,
                    pathProvider.Object
                ),
            Throws
                .TypeOf<ArgumentNullException>()
                .With.Property("ParamName")
                .EqualTo("dbConnectionFactory")
        );
    }

    [Test]
    public void Constructor_ThrowsWhenAppDataPathProviderIsNull()
    {
        var clock = new Mock<IClock>();
        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        var connectionFactory = new Mock<IDbConnectionFactory>();

        Assert.That(
            () =>
                new RuntimeAppService(
                    clock.Object,
                    startProvider.Object,
                    connectionFactory.Object,
                    null!
                ),
            Throws
                .TypeOf<ArgumentNullException>()
                .With.Property("ParamName")
                .EqualTo("appDataPathProvider")
        );
    }

    private static Mock<IDbConnectionFactory> CreateInMemorySqliteFactory(
        out SqliteConnection connection
    )
    {
        // The factory hands out the same opened in-memory connection on
        // every call so the test fixture controls its lifetime via the
        // outer `await using`. Production wiring opens a fresh
        // connection each time (per ADR-024 §E1).
        var local = new SqliteConnection("Data Source=:memory:");
        local.Open();
        connection = local;

        var factory = new Mock<IDbConnectionFactory>();
        factory
            .Setup(f => f.OpenAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(_ => Task.FromResult<DbConnection>(local));
        return factory;
    }

    private static async Task SeedSchemaVersionAsync(SqliteConnection connection, long version)
    {
        await using var ddl = connection.CreateCommand();
        ddl.CommandText =
            "CREATE TABLE __schema_version (version INTEGER NOT NULL PRIMARY KEY, applied_at TEXT NOT NULL);"
            + $"INSERT INTO __schema_version (version, applied_at) VALUES ({version}, '2026-05-01T00:00:00Z');";
        await ddl.ExecuteNonQueryAsync();
    }

    private static Mock<IAppDataPathProvider> CreatePathProvider()
    {
        var pathProvider = new Mock<IAppDataPathProvider>();
        pathProvider.Setup(p => p.GetDatabasePath()).Returns(TestDatabasePath);
        return pathProvider;
    }
}
