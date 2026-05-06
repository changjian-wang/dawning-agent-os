using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Services;
using Dawning.AgentOS.Domain.Inbox;
using Dawning.AgentOS.Domain.Memory;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InboxToMemoryAppService"/>. Per ADR-034 the
/// coordinator:
/// <list type="bullet">
///   <item>
///     <description>loads the inbox aggregate via
///     <see cref="IInboxRepository.GetByIdAsync"/> and returns
///     <c>inbox.notFound</c> when absent (§决策 G1);</description>
///   </item>
///   <item>
///     <description>calls <see cref="MemoryLedgerEntry.Create"/> directly
///     with <c>source = InboxAction</c>, <c>isExplicit = true</c>,
///     <c>confidence = 1.0</c>, <c>sensitivity = Normal</c>, and
///     <c>scope = "inbox"</c> (§决策 A1 / B1 / C1);</description>
///   </item>
///   <item>
///     <description>does not dedup — repeated promotion of the same
///     inbox item produces multiple ledger rows (§决策 F1).</description>
///   </item>
/// </list>
/// </summary>
[TestFixture]
public sealed class InboxToMemoryAppServiceTests
{
    private static readonly DateTimeOffset SampleCapturedAt = new(
        2026,
        5,
        3,
        12,
        0,
        0,
        TimeSpan.Zero
    );

    private static readonly DateTimeOffset SamplePromotedAt = new(
        2026,
        5,
        6,
        9,
        30,
        0,
        TimeSpan.Zero
    );

    [Test]
    public async Task PromoteAsync_ReturnsSuccess_WhenInboxItemExists()
    {
        var item = InboxItem.Capture("用户记录的一段话", "manual", SampleCapturedAt);
        var (sut, _, _) = BuildSut(item, SamplePromotedAt);

        var result = await sut.PromoteAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Content, Is.EqualTo(item.Content));
    }

    [Test]
    public async Task PromoteAsync_ReturnsItemNotFound_WhenInboxMissing()
    {
        var missingId = Guid.CreateVersion7();
        var inboxRepo = new Mock<IInboxRepository>();
        inboxRepo
            .Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InboxItem?)null);

        // Strict so any AddAsync call is a test failure.
        var memoryRepo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SamplePromotedAt);

        var sut = new InboxToMemoryAppService(clock.Object, inboxRepo.Object, memoryRepo.Object);

        var result = await sut.PromoteAsync(missingId, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Code, Is.EqualTo(InboxErrors.ItemNotFoundCode));
        memoryRepo.Verify(
            r => r.AddAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task PromoteAsync_StampsInboxActionSourceAndExplicitFlag()
    {
        var item = InboxItem.Capture("第二条记录", "chat", SampleCapturedAt);
        var (sut, _, captures) = BuildSut(item, SamplePromotedAt);

        var result = await sut.PromoteAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(captures, Has.Count.EqualTo(1));
        var entry = captures[0];
        Assert.That(entry.Source, Is.EqualTo(MemorySource.InboxAction));
        Assert.That(entry.IsExplicit, Is.True);
        Assert.That(entry.Confidence, Is.EqualTo(1.0));
        Assert.That(entry.Sensitivity, Is.EqualTo(MemorySensitivity.Normal));
        Assert.That(entry.Status, Is.EqualTo(MemoryStatus.Active));
    }

    [Test]
    public async Task PromoteAsync_StampsFixedInboxScope()
    {
        var item = InboxItem.Capture("scope check", "manual", SampleCapturedAt);
        var (sut, _, captures) = BuildSut(item, SamplePromotedAt);

        var result = await sut.PromoteAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(captures[0].Scope, Is.EqualTo(InboxToMemoryAppService.PromotionScope));
        Assert.That(captures[0].Scope, Is.EqualTo("inbox"));
    }

    [Test]
    public async Task PromoteAsync_PassesInboxContentThroughVerbatim()
    {
        const string sourceContent = "  原文带前后空格  和 多种 字符 !@#  ";
        var item = InboxItem.Capture(sourceContent, "chat", SampleCapturedAt);
        var (sut, _, captures) = BuildSut(item, SamplePromotedAt);

        var result = await sut.PromoteAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        // ADR-034 §决策 B1 — content is the only field copied; no trim, no rewrite.
        Assert.That(captures[0].Content, Is.EqualTo(item.Content));
        Assert.That(result.Value!.Content, Is.EqualTo(item.Content));
    }

    [Test]
    public async Task PromoteAsync_StampsCreatedAtFromClock()
    {
        var item = InboxItem.Capture("clock check", "manual", SampleCapturedAt);
        var (sut, _, captures) = BuildSut(item, SamplePromotedAt);

        var result = await sut.PromoteAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(captures[0].CreatedAt, Is.EqualTo(SamplePromotedAt));
        Assert.That(captures[0].UpdatedAtUtc, Is.EqualTo(SamplePromotedAt));
        Assert.That(result.Value!.CreatedAt, Is.EqualTo(SamplePromotedAt));
    }

    [Test]
    public async Task PromoteAsync_AllowsRepeatPromotion_AndProducesDistinctEntries()
    {
        var item = InboxItem.Capture("可以反复 promote", "manual", SampleCapturedAt);
        var inboxRepo = new Mock<IInboxRepository>();
        inboxRepo
            .Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var captures = new List<MemoryLedgerEntry>();
        var memoryRepo = new Mock<IMemoryLedgerRepository>();
        memoryRepo
            .Setup(r => r.AddAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<MemoryLedgerEntry, CancellationToken>((e, _) => captures.Add(e))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupSequence(c => c.UtcNow)
            .Returns(SamplePromotedAt)
            .Returns(SamplePromotedAt.AddMinutes(1));

        var sut = new InboxToMemoryAppService(clock.Object, inboxRepo.Object, memoryRepo.Object);

        var first = await sut.PromoteAsync(item.Id, CancellationToken.None);
        var second = await sut.PromoteAsync(item.Id, CancellationToken.None);

        Assert.That(first.IsSuccess, Is.True);
        Assert.That(second.IsSuccess, Is.True);
        Assert.That(captures, Has.Count.EqualTo(2));
        // ADR-034 §决策 F1 — no dedup; ids must differ even though content matches.
        Assert.That(captures[0].Id, Is.Not.EqualTo(captures[1].Id));
        Assert.That(captures[0].Content, Is.EqualTo(captures[1].Content));
        memoryRepo.Verify(
            r => r.AddAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Test]
    public void Constructor_ThrowsArgumentNullException_OnNullClock()
    {
        var inboxRepo = new Mock<IInboxRepository>().Object;
        var memoryRepo = new Mock<IMemoryLedgerRepository>().Object;

        Assert.That(
            () => new InboxToMemoryAppService(null!, inboxRepo, memoryRepo),
            Throws.ArgumentNullException
        );
    }

    [Test]
    public void Constructor_ThrowsArgumentNullException_OnNullInboxRepository()
    {
        var clock = new Mock<IClock>().Object;
        var memoryRepo = new Mock<IMemoryLedgerRepository>().Object;

        Assert.That(
            () => new InboxToMemoryAppService(clock, null!, memoryRepo),
            Throws.ArgumentNullException
        );
    }

    [Test]
    public void Constructor_ThrowsArgumentNullException_OnNullMemoryRepository()
    {
        var clock = new Mock<IClock>().Object;
        var inboxRepo = new Mock<IInboxRepository>().Object;

        Assert.That(
            () => new InboxToMemoryAppService(clock, inboxRepo, null!),
            Throws.ArgumentNullException
        );
    }

    [Test]
    public void PromoteAsync_PropagatesCancellation_FromInboxRepository()
    {
        var itemId = Guid.CreateVersion7();
        var inboxRepo = new Mock<IInboxRepository>();
        inboxRepo
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Strict so memoryRepo.AddAsync triggers a failure if reached.
        var memoryRepo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SamplePromotedAt);

        var sut = new InboxToMemoryAppService(clock.Object, inboxRepo.Object, memoryRepo.Object);

        Assert.That(
            async () => await sut.PromoteAsync(itemId, CancellationToken.None),
            Throws.InstanceOf<OperationCanceledException>()
        );
    }

    [Test]
    public async Task PromoteAsync_ReturnsDtoWithMatchingProjection()
    {
        var item = InboxItem.Capture("dto projection", "manual", SampleCapturedAt);
        var (sut, _, captures) = BuildSut(item, SamplePromotedAt);

        var result = await sut.PromoteAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        var dto = result.Value!;
        var entry = captures[0];
        Assert.That(dto.Id, Is.EqualTo(entry.Id));
        Assert.That(dto.Source, Is.EqualTo("InboxAction"));
        Assert.That(dto.Sensitivity, Is.EqualTo("Normal"));
        Assert.That(dto.Status, Is.EqualTo("Active"));
        Assert.That(dto.IsExplicit, Is.True);
        Assert.That(dto.Confidence, Is.EqualTo(1.0));
        Assert.That(dto.DeletedAt, Is.Null);
    }

    private static (
        InboxToMemoryAppService Sut,
        Mock<IMemoryLedgerRepository> MemoryRepo,
        List<MemoryLedgerEntry> Captures
    ) BuildSut(InboxItem item, DateTimeOffset promotedAt)
    {
        var inboxRepo = new Mock<IInboxRepository>();
        inboxRepo
            .Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var captures = new List<MemoryLedgerEntry>();
        var memoryRepo = new Mock<IMemoryLedgerRepository>();
        memoryRepo
            .Setup(r => r.AddAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<MemoryLedgerEntry, CancellationToken>((e, _) => captures.Add(e))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(promotedAt);

        var sut = new InboxToMemoryAppService(clock.Object, inboxRepo.Object, memoryRepo.Object);
        return (sut, memoryRepo, captures);
    }
}
