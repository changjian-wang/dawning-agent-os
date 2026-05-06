using Dawning.AgentOS.Application.Abstractions.Hosting;
using Dawning.AgentOS.Infrastructure.Options;
using Dawning.AgentOS.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for <see cref="SqliteConnectionFactory"/>. Per ADR-024 §6 the
/// factory must (1) honour the <c>SqliteOptions.DatabasePath</c>
/// override when set, (2) fall back to <c>IAppDataPathProvider</c> when
/// the override is empty, and (3) open connections with foreign-keys
/// enabled.
/// </summary>
[TestFixture]
public class SqliteConnectionFactoryTests
{
    [Test]
    public async Task OpenAsync_OpensSqliteConnection_WithForeignKeysEnabled()
    {
        var pathProvider = new Mock<IAppDataPathProvider>();
        var options = new OptionsWrapper<SqliteOptions>(
            new SqliteOptions { DatabasePath = "Data Source=:memory:;Cache=Shared" }
        );

        var sut = new SqliteConnectionFactory(pathProvider.Object, options);

        await using var connection = await sut.OpenAsync(CancellationToken.None);
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys;";
        var result = (long?)await pragma.ExecuteScalarAsync();
        Assert.That(result, Is.EqualTo(1L), "ForeignKeys must be enabled on the connection");
    }

    [Test]
    public async Task OpenAsync_UsesSqliteOptionsDatabasePath_WhenSet()
    {
        var pathProvider = new Mock<IAppDataPathProvider>();
        var options = new OptionsWrapper<SqliteOptions>(
            new SqliteOptions { DatabasePath = "Data Source=:memory:;Cache=Shared" }
        );

        var sut = new SqliteConnectionFactory(pathProvider.Object, options);

        await using var connection = await sut.OpenAsync(CancellationToken.None);
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));

        // Path provider must NOT be consulted when the override is set.
        pathProvider.Verify(p => p.GetDatabasePath(), Times.Never);
    }

    [Test]
    public async Task OpenAsync_FallsBackToAppDataPathProvider_WhenOptionsEmpty()
    {
        // When the override is empty the factory wraps the provider's
        // path through SqliteConnectionStringBuilder; we verify the
        // provider gets called and the connection opens against an
        // in-memory database (we route the provider to ":memory:").
        var pathProvider = new Mock<IAppDataPathProvider>();
        pathProvider.Setup(p => p.GetDatabasePath()).Returns(":memory:");

        var options = new OptionsWrapper<SqliteOptions>(new SqliteOptions { DatabasePath = null });

        var sut = new SqliteConnectionFactory(pathProvider.Object, options);

        await using var connection = await sut.OpenAsync(CancellationToken.None);
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));
        pathProvider.Verify(p => p.GetDatabasePath(), Times.Once);
    }

    [Test]
    public void Constructor_ThrowsWhenAppDataPathProviderIsNull()
    {
        var options = new OptionsWrapper<SqliteOptions>(new SqliteOptions());

        Assert.That(
            () => new SqliteConnectionFactory(null!, options),
            Throws
                .TypeOf<ArgumentNullException>()
                .With.Property("ParamName")
                .EqualTo("appDataPathProvider")
        );
    }

    [Test]
    public void Constructor_ThrowsWhenOptionsIsNull()
    {
        var pathProvider = new Mock<IAppDataPathProvider>();

        Assert.That(
            () => new SqliteConnectionFactory(pathProvider.Object, null!),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("options")
        );
    }
}
