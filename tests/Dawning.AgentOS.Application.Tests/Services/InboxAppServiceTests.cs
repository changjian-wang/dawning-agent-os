using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Services;
using Dawning.AgentOS.Domain.Inbox;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InboxAppService"/>. Per ADR-026 §6 the
/// service:
/// <list type="bullet">
///   <item>
///     <description>validates input DTOs and surfaces business errors as
///     <c>Result.Failure</c> with field-level entries that map to
///     HTTP 400;</description>
///   </item>
///   <item>
///     <description>stamps the capture instant from <see cref="IClock"/>
///     rather than trusting client-supplied timestamps;</description>
///   </item>
///   <item>
///     <description>persists via <see cref="IInboxRepository"/> and
///     drains the aggregate's domain-event queue on success (V0 has no
///     dispatcher per ADR-022 §10).</description>
///   </item>
/// </list>
/// </summary>
[TestFixture]
public sealed class InboxAppServiceTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task CaptureAsync_PersistsItemAndReturnsSnapshotWithClockStampedTimestamp()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        InboxItem? captured = null;
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<InboxItem>(), It.IsAny<CancellationToken>()))
            .Callback<InboxItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var result = await sut.CaptureAsync(
            new CaptureInboxItemRequest("hello", "chat"),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Id, Is.EqualTo(captured!.Id));
            Assert.That(result.Value.Content, Is.EqualTo("hello"));
            Assert.That(result.Value.Source, Is.EqualTo("chat"));
            Assert.That(result.Value.CapturedAtUtc, Is.EqualTo(SampleNow));
            Assert.That(result.Value.CreatedAt, Is.EqualTo(SampleNow));
        });
        repo.Verify(
            r => r.AddAsync(It.IsAny<InboxItem>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task CaptureAsync_DrainsDomainEventsOnSuccess()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        InboxItem? captured = null;
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<InboxItem>(), It.IsAny<CancellationToken>()))
            .Callback<InboxItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var sut = new InboxAppService(clock.Object, repo.Object);

        await sut.CaptureAsync(new CaptureInboxItemRequest("hello", null), CancellationToken.None);

        Assert.That(captured, Is.Not.Null);
        Assert.That(
            captured!.DomainEvents,
            Is.Empty,
            "ADR-026 §6: V0 service must drain raised events even though no dispatcher is wired."
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public async Task CaptureAsync_ReturnsFieldFailure_WhenContentIsBlank(string blank)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        var repo = new Mock<IInboxRepository>(MockBehavior.Strict);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var result = await sut.CaptureAsync(
            new CaptureInboxItemRequest(blank, null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Field, Is.EqualTo("content"));
        Assert.That(result.Errors[0].Code, Is.EqualTo("inbox.content.required"));
    }

    [Test]
    public async Task CaptureAsync_ReturnsFieldFailure_WhenContentExceedsMaxLength()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        var repo = new Mock<IInboxRepository>(MockBehavior.Strict);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var oversize = new string('a', InboxItem.MaxContentLength + 1);
        var result = await sut.CaptureAsync(
            new CaptureInboxItemRequest(oversize, null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Field, Is.EqualTo("content"));
        Assert.That(result.Errors[0].Code, Is.EqualTo("inbox.content.tooLong"));
    }

    [Test]
    public async Task CaptureAsync_ReturnsFieldFailure_WhenSourceIsWhitespace()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        var repo = new Mock<IInboxRepository>(MockBehavior.Strict);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var result = await sut.CaptureAsync(
            new CaptureInboxItemRequest("hello", "   "),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Field, Is.EqualTo("source"));
        Assert.That(result.Errors[0].Code, Is.EqualTo("inbox.source.invalid"));
    }

    [Test]
    public async Task CaptureAsync_ReturnsFieldFailure_WhenSourceExceedsMaxLength()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        var repo = new Mock<IInboxRepository>(MockBehavior.Strict);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var oversize = new string('s', InboxItem.MaxSourceLength + 1);
        var result = await sut.CaptureAsync(
            new CaptureInboxItemRequest("hello", oversize),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Field, Is.EqualTo("source"));
        Assert.That(result.Errors[0].Code, Is.EqualTo("inbox.source.tooLong"));
    }

    [Test]
    public async Task CaptureAsync_PropagatesRepositoryExceptions()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<InboxItem>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated I/O failure"));

        var sut = new InboxAppService(clock.Object, repo.Object);

        Assert.That(
            async () =>
                await sut.CaptureAsync(
                    new CaptureInboxItemRequest("hello", null),
                    CancellationToken.None
                ),
            Throws.TypeOf<InvalidOperationException>()
        );
    }

    [Test]
    public async Task ListAsync_ReturnsPageWithRepoItemsAndCount()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        var repo = new Mock<IInboxRepository>();
        var items = new[]
        {
            InboxItem.Capture("first", null, SampleNow),
            InboxItem.Capture("second", null, SampleNow.AddSeconds(-1)),
        };
        repo.Setup(r => r.ListAsync(50, 0, It.IsAny<CancellationToken>())).ReturnsAsync(items);
        repo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2L);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var result = await sut.ListAsync(new InboxListQuery(50, 0), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Items, Has.Count.EqualTo(2));
            Assert.That(result.Value.Items[0].Content, Is.EqualTo("first"));
            Assert.That(result.Value.Items[1].Content, Is.EqualTo("second"));
            Assert.That(result.Value.Total, Is.EqualTo(2L));
            Assert.That(result.Value.Limit, Is.EqualTo(50));
            Assert.That(result.Value.Offset, Is.EqualTo(0));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(InboxAppService.MaxListLimit + 1)]
    public async Task ListAsync_ReturnsFieldFailure_WhenLimitOutOfRange(int badLimit)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        var repo = new Mock<IInboxRepository>(MockBehavior.Strict);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var result = await sut.ListAsync(new InboxListQuery(badLimit, 0), CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors.Any(e => e.Field == "limit"), Is.True);
    }

    [Test]
    public async Task ListAsync_ReturnsFieldFailure_WhenOffsetIsNegative()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        var repo = new Mock<IInboxRepository>(MockBehavior.Strict);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var result = await sut.ListAsync(new InboxListQuery(50, -1), CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors.Any(e => e.Field == "offset"), Is.True);
    }

    [Test]
    public async Task ListAsync_AccumulatesMultipleFieldErrors()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        var repo = new Mock<IInboxRepository>(MockBehavior.Strict);

        var sut = new InboxAppService(clock.Object, repo.Object);

        var result = await sut.ListAsync(new InboxListQuery(0, -5), CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors, Has.Length.EqualTo(2));
    }

    [Test]
    public void Constructor_ThrowsWhenClockIsNull()
    {
        var repo = new Mock<IInboxRepository>();

        Assert.That(
            () => new InboxAppService(null!, repo.Object),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("clock")
        );
    }

    [Test]
    public void Constructor_ThrowsWhenRepositoryIsNull()
    {
        var clock = new Mock<IClock>();

        Assert.That(
            () => new InboxAppService(clock.Object, null!),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("repository")
        );
    }
}
