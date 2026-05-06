using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Inbox;
using Dawning.AgentOS.Infrastructure.Persistence;
using Dawning.AgentOS.Infrastructure.Persistence.Inbox;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Persistence.Inbox;

/// <summary>
/// Tests for <see cref="InboxRepository"/>. Per ADR-026 §5 the V0
/// repository is a thin Dapper layer over <see cref="IDbConnectionFactory"/>;
/// these tests run against a shared in-memory SQLite store with the
/// schema bootstrap applied via <see cref="SqliteSchemaInitializer"/>
/// (so the suite also exercises migration <c>0002_create_inbox_items.sql</c>
/// against the real engine).
/// </summary>
[TestFixture]
public sealed class InboxRepositoryTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AddAsync_PersistsRow_AndCountIncrements()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new InboxRepository(factory);
        var item = InboxItem.Capture("hello", "chat", SampleNow);

        await sut.AddAsync(item, CancellationToken.None);

        var total = await sut.CountAsync(CancellationToken.None);
        Assert.That(total, Is.EqualTo(1));
    }

    [Test]
    public async Task ListAsync_ReturnsItemsOrderedByCapturedAtDescending()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new InboxRepository(factory);
        // Captured in arbitrary order; expect ListAsync to surface DESC.
        var older = InboxItem.Capture("older", null, SampleNow.AddMinutes(-10));
        var newer = InboxItem.Capture("newer", null, SampleNow);
        var middle = InboxItem.Capture("middle", null, SampleNow.AddMinutes(-5));

        await sut.AddAsync(older, CancellationToken.None);
        await sut.AddAsync(newer, CancellationToken.None);
        await sut.AddAsync(middle, CancellationToken.None);

        var page = await sut.ListAsync(50, 0, CancellationToken.None);

        Assert.That(page, Has.Count.EqualTo(3));
        Assert.That(page[0].Content, Is.EqualTo("newer"));
        Assert.That(page[1].Content, Is.EqualTo("middle"));
        Assert.That(page[2].Content, Is.EqualTo("older"));
    }

    [Test]
    public async Task ListAsync_AppliesLimitAndOffset()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new InboxRepository(factory);
        for (var i = 0; i < 5; i++)
        {
            await sut.AddAsync(
                InboxItem.Capture($"item-{i}", null, SampleNow.AddMinutes(-i)),
                CancellationToken.None
            );
        }

        var firstPage = await sut.ListAsync(2, 0, CancellationToken.None);
        var secondPage = await sut.ListAsync(2, 2, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstPage, Has.Count.EqualTo(2));
            Assert.That(firstPage[0].Content, Is.EqualTo("item-0"));
            Assert.That(firstPage[1].Content, Is.EqualTo("item-1"));
            Assert.That(secondPage, Has.Count.EqualTo(2));
            Assert.That(secondPage[0].Content, Is.EqualTo("item-2"));
            Assert.That(secondPage[1].Content, Is.EqualTo("item-3"));
        });
    }

    [Test]
    public async Task ListAsync_ReturnsEmpty_WhenStoreIsEmpty()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new InboxRepository(factory);

        var page = await sut.ListAsync(50, 0, CancellationToken.None);
        var total = await sut.CountAsync(CancellationToken.None);

        Assert.That(page, Is.Empty);
        Assert.That(total, Is.EqualTo(0));
    }

    [Test]
    public async Task RoundTrip_PreservesAllPersistedFields_IncludingNullSource()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new InboxRepository(factory);
        var item = InboxItem.Capture("payload", source: null, SampleNow);
        await sut.AddAsync(item, CancellationToken.None);

        var page = await sut.ListAsync(1, 0, CancellationToken.None);
        Assert.That(page, Has.Count.EqualTo(1));
        var persisted = page[0];
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(item.Id));
            Assert.That(persisted.Content, Is.EqualTo("payload"));
            Assert.That(persisted.Source, Is.Null);
            Assert.That(persisted.CapturedAtUtc, Is.EqualTo(SampleNow));
            Assert.That(persisted.CreatedAt, Is.EqualTo(SampleNow));
            Assert.That(
                persisted.DomainEvents,
                Is.Empty,
                "ADR-022: rehydration must not raise events"
            );
        });
    }

    [Test]
    public void Constructor_ThrowsWhenConnectionFactoryIsNull()
    {
        Assert.That(
            () => new InboxRepository(null!),
            Throws
                .TypeOf<ArgumentNullException>()
                .With.Property("ParamName")
                .EqualTo("connectionFactory")
        );
    }

    [Test]
    public void AddAsync_ThrowsWhenItemIsNull()
    {
        var factory = new Mock<IDbConnectionFactory>(MockBehavior.Strict).Object;
        var sut = new InboxRepository(factory);

        Assert.That(
            async () => await sut.AddAsync(null!, CancellationToken.None),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("item")
        );
    }

    private static SqliteConnection OpenSharedInMemoryConnection()
    {
        var connectionString =
            $"Data Source=inbox-repo-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static IDbConnectionFactory CreateFactoryFor(SqliteConnection keepAlive)
    {
        var mock = new Mock<IDbConnectionFactory>();
        mock.Setup(f => f.OpenAsync(It.IsAny<CancellationToken>()))
            .Returns(
                async (CancellationToken token) =>
                {
                    var conn = new SqliteConnection(keepAlive.ConnectionString);
                    await conn.OpenAsync(token);
                    return conn;
                }
            );
        return mock.Object;
    }

    private static async Task ApplySchemaAsync(IDbConnectionFactory factory)
    {
        // Dapper auto-mapping is enabled inside AddInfrastructure(); the
        // tests must opt in explicitly because they construct repositories
        // by hand without hitting the DI extension.
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var initializer = new SqliteSchemaInitializer(
            factory,
            NullLogger<SqliteSchemaInitializer>.Instance
        );
        await initializer.InitializeAsync(CancellationToken.None);
    }
}
