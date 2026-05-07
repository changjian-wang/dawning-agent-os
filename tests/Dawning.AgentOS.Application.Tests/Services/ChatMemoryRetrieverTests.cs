using Dawning.AgentOS.Application.Services;
using Dawning.AgentOS.Domain.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Pins the silent-degrade and tokenize-then-search wiring of
/// <see cref="ChatMemoryRetriever"/> per ADR-038 §决策 A1 / C1 / E1 / F1.
/// </summary>
[TestFixture]
public sealed class ChatMemoryRetrieverTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Ctor_NullRepository_Throws()
    {
        Assert.That(
            () => new ChatMemoryRetriever(null!, NullLogger<ChatMemoryRetriever>.Instance),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("memoryRepository")
        );
    }

    [Test]
    public void Ctor_NullLogger_Throws()
    {
        Assert.That(
            () => new ChatMemoryRetriever(Mock.Of<IMemoryLedgerRepository>(), null!),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("logger")
        );
    }

    [Test]
    public void RetrieveAsync_NullUserMessage_Throws()
    {
        var sut = new ChatMemoryRetriever(
            Mock.Of<IMemoryLedgerRepository>(),
            NullLogger<ChatMemoryRetriever>.Instance
        );

        Assert.That(
            async () => await sut.RetrieveAsync(null!, CancellationToken.None),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("userMessage")
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("    \t  ")]
    public async Task RetrieveAsync_BlankUserMessage_ReturnsEmpty_WithoutCallingRepository(
        string blank
    )
    {
        var repo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        var sut = new ChatMemoryRetriever(repo.Object, NullLogger<ChatMemoryRetriever>.Instance);

        var result = await sut.RetrieveAsync(blank, CancellationToken.None);

        Assert.That(result, Is.Empty);
        repo.Verify(
            r => r.SearchByKeywordsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Test]
    public async Task RetrieveAsync_UserMessageProducesNoKeywords_ReturnsEmpty_WithoutCallingRepository()
    {
        var repo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        var sut = new ChatMemoryRetriever(repo.Object, NullLogger<ChatMemoryRetriever>.Instance);

        // Single ASCII char + single CJK char + punctuation ⇒ no
        // keyword passes the §A1 filters.
        var result = await sut.RetrieveAsync("a 好 ?? !", CancellationToken.None);

        Assert.That(result, Is.Empty);
        repo.Verify(
            r => r.SearchByKeywordsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Test]
    public async Task RetrieveAsync_HappyPath_ForwardsKeywordsAndCap_ToRepository()
    {
        var entry = NewEntry("我喜欢吃苹果。");
        IReadOnlyList<string>? capturedKeywords = null;
        int capturedTake = -1;

        var repo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        repo.Setup(r =>
                r.SearchByKeywordsAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IReadOnlyList<string>, int, CancellationToken>(
                (kws, take, _) =>
                {
                    capturedKeywords = kws;
                    capturedTake = take;
                }
            )
            .ReturnsAsync(new[] { entry });

        var sut = new ChatMemoryRetriever(repo.Object, NullLogger<ChatMemoryRetriever>.Instance);

        var result = await sut.RetrieveAsync("我喜欢苹果", CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.SameAs(entry));
        Assert.That(capturedTake, Is.EqualTo(ChatMemoryRetriever.MaxRetrievedEntries));
        Assert.That(capturedKeywords, Is.Not.Null);
        // Sliding 2-grams over the 4-CJK run with no breaks: "我喜",
        // "喜欢", "欢苹", "苹果".
        Assert.That(
            capturedKeywords!,
            Is.EquivalentTo(new[] { "我喜", "喜欢", "欢苹", "苹果" })
        );
    }

    [Test]
    public async Task RetrieveAsync_RepositoryThrows_LogsWarningAndReturnsEmpty()
    {
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r =>
                r.SearchByKeywordsAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("storage offline"));

        var logger = new Mock<ILogger<ChatMemoryRetriever>>();
        var sut = new ChatMemoryRetriever(repo.Object, logger.Object);

        var result = await sut.RetrieveAsync("hello memory", CancellationToken.None);

        Assert.That(result, Is.Empty);
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(ex => ex is InvalidOperationException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Test]
    public void RetrieveAsync_OperationCancelled_PropagatesUnchanged()
    {
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r =>
                r.SearchByKeywordsAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());

        var sut = new ChatMemoryRetriever(repo.Object, NullLogger<ChatMemoryRetriever>.Instance);

        Assert.That(
            async () => await sut.RetrieveAsync("hello", CancellationToken.None),
            Throws.TypeOf<OperationCanceledException>()
        );
    }

    private static MemoryLedgerEntry NewEntry(string content) =>
        MemoryLedgerEntry.Create(
            content: content,
            scope: null,
            source: MemorySource.UserExplicit,
            isExplicit: true,
            confidence: 1.0,
            sensitivity: MemorySensitivity.Normal,
            createdAtUtc: SampleNow
        );
}
