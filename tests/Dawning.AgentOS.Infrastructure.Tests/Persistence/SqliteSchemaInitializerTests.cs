using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for <see cref="SqliteSchemaInitializer"/>. Per ADR-024 §6 the
/// initializer must (1) bootstrap the <c>__schema_version</c> tracking
/// table on first run, (2) be idempotent across restarts, and (3) record
/// each applied migration with an ISO-8601 UTC timestamp.
/// </summary>
[TestFixture]
public class SqliteSchemaInitializerTests
{
    [Test]
    public async Task InitializeAsync_AppliesSeedMigration_OnFirstRun()
    {
        await using var connection = OpenSharedInMemoryConnection();

        var sut = CreateInitializer(connection);
        await sut.InitializeAsync(CancellationToken.None);

        var versions = await ReadVersionsAsync(connection);
        Assert.That(versions, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(versions[0], Is.EqualTo(1L), "the seed migration must be 0001");
    }

    [Test]
    public async Task InitializeAsync_IsIdempotent_OnSecondRun()
    {
        await using var connection = OpenSharedInMemoryConnection();

        var sut = CreateInitializer(connection);
        await sut.InitializeAsync(CancellationToken.None);
        var firstPass = await ReadVersionsAsync(connection);

        await sut.InitializeAsync(CancellationToken.None);
        var secondPass = await ReadVersionsAsync(connection);

        Assert.That(secondPass, Is.EqualTo(firstPass), "second run must not insert duplicate rows");
    }

    [Test]
    public async Task InitializeAsync_WritesAppliedAtAsIso8601Utc()
    {
        await using var connection = OpenSharedInMemoryConnection();

        var sut = CreateInitializer(connection);
        await sut.InitializeAsync(CancellationToken.None);

        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT applied_at FROM __schema_version WHERE version = 1";
        var raw = (string?)await query.ExecuteScalarAsync();

        Assert.That(raw, Is.Not.Null.And.Not.Empty);
        Assert.That(
            DateTimeOffset.TryParse(
                raw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal
                    | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed
            ),
            Is.True,
            $"applied_at must be ISO-8601 round-trippable, got '{raw}'"
        );
        Assert.That(
            parsed,
            Is.EqualTo(DateTimeOffset.UtcNow).Within(TimeSpan.FromMinutes(5)),
            "applied_at should reflect the wall-clock time of the run"
        );
    }

    private static SqliteConnection OpenSharedInMemoryConnection()
    {
        // The schema initializer opens its own connections through the
        // factory; we therefore back the test by a shared in-memory
        // database. The outer `await using` keeps the keep-alive
        // connection open for the whole test, so the in-memory store
        // survives between factory.OpenAsync() calls.
        var connectionString =
            $"Data Source=schema-init-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static SqliteSchemaInitializer CreateInitializer(SqliteConnection keepAliveConnection)
    {
        var factory = new Mock<IDbConnectionFactory>();
        factory
            .Setup(f => f.OpenAsync(It.IsAny<CancellationToken>()))
            .Returns(
                async (CancellationToken token) =>
                {
                    var conn = new SqliteConnection(keepAliveConnection.ConnectionString);
                    await conn.OpenAsync(token);
                    return conn;
                }
            );

        return new SqliteSchemaInitializer(
            factory.Object,
            NullLogger<SqliteSchemaInitializer>.Instance
        );
    }

    private static async Task<List<long>> ReadVersionsAsync(SqliteConnection connection)
    {
        var versions = new List<long>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM __schema_version ORDER BY version ASC";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            versions.Add(reader.GetInt64(0));
        }
        return versions;
    }
}
