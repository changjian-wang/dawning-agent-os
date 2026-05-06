using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Domain.Chat;
using Dawning.AgentOS.Infrastructure.Persistence;
using Dawning.AgentOS.Infrastructure.Persistence.Chat;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Persistence.Chat;

/// <summary>
/// Tests for <see cref="ChatSessionRepository"/>. Per ADR-032 §决策 D2 /
/// L1 the V0 repository is a thin Dapper layer over
/// <see cref="IDbConnectionFactory"/>; these tests run against a shared
/// in-memory SQLite store with the schema bootstrap applied via
/// <see cref="SqliteSchemaInitializer"/> (so the suite also exercises
/// migration <c>0003_create_chat_tables.sql</c> against the real
/// engine).
/// </summary>
[TestFixture]
public sealed class ChatSessionRepositoryTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AddAsync_RoundTripsAllFields()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);
        var session = ChatSession.Create(SampleNow);
        session.SetTitleFromFirstMessage("first user message", SampleNow.AddSeconds(1));

        await sut.AddAsync(session, CancellationToken.None);
        var fetched = await sut.GetAsync(session.Id, CancellationToken.None);

        Assert.That(fetched, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fetched!.Id, Is.EqualTo(session.Id));
            Assert.That(fetched.Title, Is.EqualTo(session.Title));
            Assert.That(fetched.CreatedAt, Is.EqualTo(session.CreatedAt));
            Assert.That(fetched.UpdatedAt, Is.EqualTo(session.UpdatedAt));
        });
    }

    [Test]
    public async Task GetAsync_ReturnsNull_ForUnknownId()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);

        var fetched = await sut.GetAsync(Guid.CreateVersion7(SampleNow), CancellationToken.None);

        Assert.That(fetched, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_PersistsTitleAndUpdatedAtChanges()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);
        var session = ChatSession.Create(SampleNow);
        await sut.AddAsync(session, CancellationToken.None);

        session.SetTitleFromFirstMessage("hello world", SampleNow.AddMinutes(1));
        await sut.UpdateAsync(session, CancellationToken.None);

        var fetched = await sut.GetAsync(session.Id, CancellationToken.None);
        Assert.That(fetched, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fetched!.Title, Is.EqualTo("hello world"));
            Assert.That(fetched.UpdatedAt, Is.EqualTo(SampleNow.AddMinutes(1)));
            Assert.That(
                fetched.CreatedAt,
                Is.EqualTo(SampleNow),
                "created_at must not change on update."
            );
        });
    }

    [Test]
    public async Task ListAsync_OrdersByUpdatedAtDescending()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);
        var older = ChatSession.Create(SampleNow.AddMinutes(-10));
        var newer = ChatSession.Create(SampleNow);
        var middle = ChatSession.Create(SampleNow.AddMinutes(-5));

        await sut.AddAsync(older, CancellationToken.None);
        await sut.AddAsync(newer, CancellationToken.None);
        await sut.AddAsync(middle, CancellationToken.None);

        var page = await sut.ListAsync(50, 0, CancellationToken.None);

        Assert.That(page, Has.Count.EqualTo(3));
        Assert.That(page[0].Id, Is.EqualTo(newer.Id));
        Assert.That(page[1].Id, Is.EqualTo(middle.Id));
        Assert.That(page[2].Id, Is.EqualTo(older.Id));
    }

    [Test]
    public async Task ListAsync_AppliesLimitAndOffset()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);
        for (var i = 0; i < 5; i++)
        {
            await sut.AddAsync(
                ChatSession.Create(SampleNow.AddMinutes(-i)),
                CancellationToken.None
            );
        }

        var firstPage = await sut.ListAsync(2, 0, CancellationToken.None);
        var secondPage = await sut.ListAsync(2, 2, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstPage, Has.Count.EqualTo(2));
            Assert.That(secondPage, Has.Count.EqualTo(2));
            Assert.That(
                firstPage.Select(s => s.Id),
                Is.Not.EquivalentTo(secondPage.Select(s => s.Id)),
                "Paged results must not overlap."
            );
        });
    }

    [Test]
    public async Task AddMessageAsync_RoundTripsUserAndAssistantTurns()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);
        var session = ChatSession.Create(SampleNow);
        await sut.AddAsync(session, CancellationToken.None);

        var userMsg = ChatMessage.CreateUser(session.Id, "你好", SampleNow.AddSeconds(1));
        var assistantMsg = ChatMessage.CreateAssistant(
            sessionId: session.Id,
            content: "你好，主人。",
            model: "gpt-4.1",
            promptTokens: 12,
            completionTokens: 4,
            createdAtUtc: SampleNow.AddSeconds(2)
        );

        await sut.AddMessageAsync(userMsg, CancellationToken.None);
        await sut.AddMessageAsync(assistantMsg, CancellationToken.None);

        var loaded = await sut.LoadMessagesAsync(session.Id, CancellationToken.None);

        Assert.That(loaded, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(loaded[0].Role, Is.EqualTo(ChatRole.User));
            Assert.That(loaded[0].Content, Is.EqualTo("你好"));
            Assert.That(loaded[0].Model, Is.Null);
            Assert.That(loaded[0].PromptTokens, Is.Null);
            Assert.That(loaded[1].Role, Is.EqualTo(ChatRole.Assistant));
            Assert.That(loaded[1].Content, Is.EqualTo("你好，主人。"));
            Assert.That(loaded[1].Model, Is.EqualTo("gpt-4.1"));
            Assert.That(loaded[1].PromptTokens, Is.EqualTo(12));
            Assert.That(loaded[1].CompletionTokens, Is.EqualTo(4));
            Assert.That(loaded[1].CreatedAt, Is.EqualTo(assistantMsg.CreatedAt));
        });
    }

    [Test]
    public async Task LoadMessagesAsync_ScopesToSessionId()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);
        var sessionA = ChatSession.Create(SampleNow);
        var sessionB = ChatSession.Create(SampleNow.AddSeconds(1));
        await sut.AddAsync(sessionA, CancellationToken.None);
        await sut.AddAsync(sessionB, CancellationToken.None);

        await sut.AddMessageAsync(
            ChatMessage.CreateUser(sessionA.Id, "in A", SampleNow.AddSeconds(2)),
            CancellationToken.None
        );
        await sut.AddMessageAsync(
            ChatMessage.CreateUser(sessionB.Id, "in B", SampleNow.AddSeconds(3)),
            CancellationToken.None
        );

        var aMessages = await sut.LoadMessagesAsync(sessionA.Id, CancellationToken.None);
        var bMessages = await sut.LoadMessagesAsync(sessionB.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(aMessages, Has.Count.EqualTo(1));
            Assert.That(aMessages[0].Content, Is.EqualTo("in A"));
            Assert.That(bMessages, Has.Count.EqualTo(1));
            Assert.That(bMessages[0].Content, Is.EqualTo("in B"));
        });
    }

    [Test]
    public async Task LoadMessagesAsync_OrdersByCreatedAtAscending()
    {
        await using var keepAlive = OpenSharedInMemoryConnection();
        var factory = CreateFactoryFor(keepAlive);
        await ApplySchemaAsync(factory);

        var sut = new ChatSessionRepository(factory);
        var session = ChatSession.Create(SampleNow);
        await sut.AddAsync(session, CancellationToken.None);

        // Insert out of chronological order to prove the ORDER BY clause works.
        await sut.AddMessageAsync(
            ChatMessage.CreateUser(session.Id, "third", SampleNow.AddSeconds(3)),
            CancellationToken.None
        );
        await sut.AddMessageAsync(
            ChatMessage.CreateUser(session.Id, "first", SampleNow.AddSeconds(1)),
            CancellationToken.None
        );
        await sut.AddMessageAsync(
            ChatMessage.CreateUser(session.Id, "second", SampleNow.AddSeconds(2)),
            CancellationToken.None
        );

        var loaded = await sut.LoadMessagesAsync(session.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded[0].Content, Is.EqualTo("first"));
            Assert.That(loaded[1].Content, Is.EqualTo("second"));
            Assert.That(loaded[2].Content, Is.EqualTo("third"));
        });
    }

    private static SqliteConnection OpenSharedInMemoryConnection()
    {
        var connectionString = $"Data Source=chat-repo-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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
