using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Memory;
using Dawning.AgentOS.Infrastructure.Persistence;
using Dawning.AgentOS.Infrastructure.Persistence.Memory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Persistence.Memory;

/// <summary>
/// Tests for <see cref="MemoryLedgerRepository"/>. Per ADR-033 §决策 L1
/// each test runs against a shared in-memory SQLite store with the
/// schema bootstrap applied via <see cref="SqliteSchemaInitializer"/>,
/// which exercises migration <c>0004_create_memory_entries.sql</c>
/// against the real engine.
/// </summary>
[TestFixture]
public sealed class MemoryLedgerRepositoryTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AddAsync_PersistsRow_AndCountIncrements()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var entry = NewEntry(content: "remember tea", at: SampleNow);

        await sut.AddAsync(entry, CancellationToken.None);

        var total = await sut.CountAsync(null, false, CancellationToken.None);
        Assert.That(total, Is.EqualTo(1));
    }

    [Test]
    public async Task RoundTrip_PreservesAllColumnsIncludingDeletedAt()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var entry = NewEntry(content: "x", at: SampleNow, sensitivity: MemorySensitivity.Sensitive);
        entry.SoftDelete(SampleNow.AddMinutes(5));

        await sut.AddAsync(entry, CancellationToken.None);

        var fetched = await sut.GetByIdAsync(entry.Id, CancellationToken.None);

        Assert.That(fetched, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fetched!.Id, Is.EqualTo(entry.Id));
            Assert.That(fetched.Content, Is.EqualTo("x"));
            Assert.That(fetched.Scope, Is.EqualTo(MemoryLedgerEntry.DefaultScope));
            Assert.That(fetched.Source, Is.EqualTo(MemorySource.UserExplicit));
            Assert.That(fetched.IsExplicit, Is.True);
            Assert.That(fetched.Confidence, Is.EqualTo(1.0));
            Assert.That(fetched.Sensitivity, Is.EqualTo(MemorySensitivity.Sensitive));
            Assert.That(fetched.Status, Is.EqualTo(MemoryStatus.SoftDeleted));
            Assert.That(fetched.CreatedAt, Is.EqualTo(SampleNow));
            Assert.That(fetched.UpdatedAtUtc, Is.EqualTo(SampleNow.AddMinutes(5)));
            Assert.That(fetched.DeletedAtUtc, Is.EqualTo(SampleNow.AddMinutes(5)));
            Assert.That(fetched.DomainEvents, Is.Empty);
        });
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNull_WhenIdMissing()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);

        var fetched = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(fetched, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_PersistsContentScopeStatusAndUpdatedAt()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var entry = NewEntry("first", SampleNow);
        await sut.AddAsync(entry, CancellationToken.None);

        entry.UpdateContent("second", SampleNow.AddMinutes(10));
        entry.UpdateScope("project:y", SampleNow.AddMinutes(11));
        entry.MarkCorrected(SampleNow.AddMinutes(12));

        var rowUpdated = await sut.UpdateAsync(entry, CancellationToken.None);
        Assert.That(rowUpdated, Is.True);

        var fetched = await sut.GetByIdAsync(entry.Id, CancellationToken.None);
        Assert.That(fetched, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fetched!.Content, Is.EqualTo("second"));
            Assert.That(fetched.Scope, Is.EqualTo("project:y"));
            Assert.That(fetched.Status, Is.EqualTo(MemoryStatus.Corrected));
            Assert.That(fetched.UpdatedAtUtc, Is.EqualTo(SampleNow.AddMinutes(12)));
        });
    }

    [Test]
    public async Task UpdateAsync_ReturnsFalse_WhenIdMissing()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var ghost = NewEntry("ghost", SampleNow);

        var rowUpdated = await sut.UpdateAsync(ghost, CancellationToken.None);

        Assert.That(rowUpdated, Is.False);
    }

    [Test]
    public async Task ListAsync_OrderedByUpdatedAtDescIdDesc()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var older = NewEntry("older", SampleNow.AddMinutes(-10));
        var middle = NewEntry("middle", SampleNow.AddMinutes(-5));
        var newer = NewEntry("newer", SampleNow);

        await sut.AddAsync(older, CancellationToken.None);
        await sut.AddAsync(newer, CancellationToken.None);
        await sut.AddAsync(middle, CancellationToken.None);

        var page = await sut.ListAsync(null, false, 50, 0, CancellationToken.None);

        Assert.That(page, Has.Count.EqualTo(3));
        Assert.That(page[0].Content, Is.EqualTo("newer"));
        Assert.That(page[1].Content, Is.EqualTo("middle"));
        Assert.That(page[2].Content, Is.EqualTo("older"));
    }

    [Test]
    public async Task ListAsync_DefaultExcludesSoftDeleted()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var alive = NewEntry("alive", SampleNow);
        var dead = NewEntry("dead", SampleNow.AddMinutes(-1));
        dead.SoftDelete(SampleNow);

        await sut.AddAsync(alive, CancellationToken.None);
        await sut.AddAsync(dead, CancellationToken.None);

        var page = await sut.ListAsync(null, false, 50, 0, CancellationToken.None);
        var total = await sut.CountAsync(null, false, CancellationToken.None);

        Assert.That(page, Has.Count.EqualTo(1));
        Assert.That(page[0].Content, Is.EqualTo("alive"));
        Assert.That(total, Is.EqualTo(1));
    }

    [Test]
    public async Task ListAsync_IncludeSoftDeletedTrue_ReturnsAllStatuses()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var alive = NewEntry("alive", SampleNow);
        var dead = NewEntry("dead", SampleNow.AddMinutes(-1));
        dead.SoftDelete(SampleNow);

        await sut.AddAsync(alive, CancellationToken.None);
        await sut.AddAsync(dead, CancellationToken.None);

        var page = await sut.ListAsync(null, true, 50, 0, CancellationToken.None);
        var total = await sut.CountAsync(null, true, CancellationToken.None);

        Assert.That(page, Has.Count.EqualTo(2));
        Assert.That(total, Is.EqualTo(2));
    }

    [Test]
    public async Task ListAsync_StatusFilterReturnsOnlyMatchingRows()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        var alive = NewEntry("alive", SampleNow);
        var archived = NewEntry("archived", SampleNow.AddMinutes(-1));
        archived.Archive(SampleNow);

        await sut.AddAsync(alive, CancellationToken.None);
        await sut.AddAsync(archived, CancellationToken.None);

        var archivedOnly = await sut.ListAsync(
            MemoryStatus.Archived,
            includeSoftDeleted: false,
            50,
            0,
            CancellationToken.None
        );
        var archivedTotal = await sut.CountAsync(
            MemoryStatus.Archived,
            includeSoftDeleted: false,
            CancellationToken.None
        );

        Assert.That(archivedOnly, Has.Count.EqualTo(1));
        Assert.That(archivedOnly[0].Content, Is.EqualTo("archived"));
        Assert.That(archivedTotal, Is.EqualTo(1));
    }

    [Test]
    public async Task ListAsync_AppliesLimitAndOffset()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new MemoryLedgerRepository(factory);
        for (var i = 0; i < 5; i++)
        {
            var e = NewEntry($"item-{i}", SampleNow.AddMinutes(-i));
            await sut.AddAsync(e, CancellationToken.None);
        }

        var firstPage = await sut.ListAsync(null, false, 2, 0, CancellationToken.None);
        var secondPage = await sut.ListAsync(null, false, 2, 2, CancellationToken.None);

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
    public void Constructor_ThrowsWhenConnectionFactoryIsNull()
    {
        Assert.That(
            () => new MemoryLedgerRepository(null!),
            Throws
                .TypeOf<ArgumentNullException>()
                .With.Property("ParamName")
                .EqualTo("connectionFactory")
        );
    }

    [Test]
    public void AddAsync_ThrowsWhenEntryIsNull()
    {
        var factory = new Mock<IDbConnectionFactory>(MockBehavior.Strict).Object;
        var sut = new MemoryLedgerRepository(factory);

        Assert.That(
            async () => await sut.AddAsync(null!, CancellationToken.None),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("entry")
        );
    }

    private static MemoryLedgerEntry NewEntry(
        string content,
        DateTimeOffset at,
        MemorySensitivity sensitivity = MemorySensitivity.Normal
    ) =>
        MemoryLedgerEntry.Create(
            content,
            null,
            MemorySource.UserExplicit,
            true,
            1.0,
            sensitivity,
            at
        );

    private static SqliteConnection OpenSharedInMemoryConnection()
    {
        var connectionString =
            $"Data Source=memory-repo-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var initializer = new SqliteSchemaInitializer(
            factory,
            NullLogger<SqliteSchemaInitializer>.Instance
        );
        await initializer.InitializeAsync(CancellationToken.None);
    }
}
