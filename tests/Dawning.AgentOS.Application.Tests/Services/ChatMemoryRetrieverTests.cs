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

    // ────────────────────────────────────────────────────────────────
    // ADR-039 §可机器化判据 — IsLikelyQuestion edge cases
    // ────────────────────────────────────────────────────────────────

    [TestCase("以什么方式保存下来？", Description = "ADR-039 §可机器化判据 1: CJK fullwidth ？ at end")]
    [TestCase("保存后会怎样?", Description = "ADR-039 §可机器化判据 1: ASCII ? at end")]
    [TestCase("以什么方式保存下来？  ", Description = "ADR-039 §可机器化判据 1: trailing whitespace, then ？")]
    [TestCase("以什么方式保存下来？\n", Description = "ADR-039 §可机器化判据 1: trailing newline, then ？")]
    public void IsLikelyQuestion_TrailingQuestionMark_ReturnsTrue(string content)
    {
        Assert.That(ChatMemoryRetriever.IsLikelyQuestion(content), Is.True);
    }

    [TestCase("我目前在用 .NET 10 + Electron 开发桌面应用 dawning-agent-os", Description = "ADR-039 §可机器化判据 2: fact, no ?")]
    [TestCase("我有 3 个项目? 不对，是 4 个", Description = "ADR-039 §可机器化判据 2: ? in middle, not trailing")]
    [TestCase("", Description = "ADR-039 §可机器化判据 2: empty string")]
    [TestCase("   ", Description = "ADR-039 §可机器化判据 2: whitespace only")]
    [TestCase("\t\n", Description = "ADR-039 §可机器化判据 2: control whitespace only")]
    public void IsLikelyQuestion_NoTrailingQuestionMark_ReturnsFalse(string content)
    {
        Assert.That(ChatMemoryRetriever.IsLikelyQuestion(content), Is.False);
    }

    [Test]
    public void IsLikelyQuestion_Null_ReturnsFalse()
    {
        Assert.That(ChatMemoryRetriever.IsLikelyQuestion(null), Is.False);
    }

    [Test]
    public async Task RetrieveAsync_RepositoryReturnsMixedFactAndQuestion_FiltersOutQuestions()
    {
        // ADR-039 §可机器化判据 3a: integration path — Repo returns
        // [fact_a, question_b, fact_c]; RetrieveAsync returns
        // [fact_a, fact_c] preserving repo ordering.
        var factA = NewEntry("我目前在用 .NET 10 + Electron 开发桌面应用 dawning-agent-os");
        var questionB = NewEntry("以什么方式保存下来？");
        var factC = NewEntry("dawning-agent-os 的桌面端用 Electron + Vite");

        var repo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        repo.Setup(r =>
                r.SearchByKeywordsAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { factA, questionB, factC });

        var sut = new ChatMemoryRetriever(repo.Object, NullLogger<ChatMemoryRetriever>.Instance);

        var result = await sut.RetrieveAsync("我用什么技术栈开发桌面应用", CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.SameAs(factA));
        Assert.That(result[1], Is.SameAs(factC));
        Assert.That(result.Any(e => e.Id == questionB.Id), Is.False);
    }

    [Test]
    public async Task RetrieveAsync_RepositoryReturnsAllQuestions_ReturnsEmpty()
    {
        // ADR-039 §可机器化判据 3b: integration path — Repo returns all
        // question-shaped entries; RetrieveAsync returns Empty
        // (acceptable truncation per §决策 C1, silent per §决策 D1).
        var q1 = NewEntry("以什么方式保存下来？");
        var q2 = NewEntry("我下次去日本玩什么?");

        var repo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        repo.Setup(r =>
                r.SearchByKeywordsAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { q1, q2 });

        var sut = new ChatMemoryRetriever(repo.Object, NullLogger<ChatMemoryRetriever>.Instance);

        var result = await sut.RetrieveAsync("我用什么技术栈", CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task RetrieveAsync_StillPassesMaxRetrievedEntries_ToRepository_AfterAdr039()
    {
        // ADR-039 §决策 A1: Repo limit unchanged at MaxRetrievedEntries.
        // Filter happens after Repo returns; the cap is not lowered to
        // compensate for filtered entries (acceptable truncation).
        int capturedTake = -1;
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r =>
                r.SearchByKeywordsAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IReadOnlyList<string>, int, CancellationToken>(
                (_, take, _) => capturedTake = take
            )
            .ReturnsAsync(Array.Empty<MemoryLedgerEntry>());

        var sut = new ChatMemoryRetriever(repo.Object, NullLogger<ChatMemoryRetriever>.Instance);

        _ = await sut.RetrieveAsync("hello memory", CancellationToken.None);

        Assert.That(capturedTake, Is.EqualTo(ChatMemoryRetriever.MaxRetrievedEntries));
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
