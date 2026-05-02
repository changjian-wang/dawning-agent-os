using Dawning.AgentOS.Domain.Inbox;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Tests.Inbox;

/// <summary>
/// Tests for the <see cref="InboxItem"/> aggregate root. Per ADR-026 §1
/// the V0 invariants are: non-empty content ≤ 4096 chars, optional
/// non-whitespace source ≤ 64 chars, UTC <c>capturedAtUtc</c>, and a
/// UUIDv7 identifier anchored to the capture instant. Per ADR-022
/// invariant violations throw <see cref="ArgumentException"/>; business
/// failures are not modeled here because the only business rule (input
/// validation) is enforced at the AppService boundary.
/// </summary>
[TestFixture]
public sealed class InboxItemTests
{
    private static readonly DateTimeOffset SampleCapturedAt = new(
        2026,
        5,
        2,
        12,
        0,
        0,
        TimeSpan.Zero
    );

    [Test]
    public void Capture_PopulatesPropertiesAndStampsUuidV7Id()
    {
        var item = InboxItem.Capture("hello world", "chat", SampleCapturedAt);

        Assert.Multiple(() =>
        {
            Assert.That(item.Content, Is.EqualTo("hello world"));
            Assert.That(item.Source, Is.EqualTo("chat"));
            Assert.That(item.CapturedAtUtc, Is.EqualTo(SampleCapturedAt));
            Assert.That(
                item.CreatedAt,
                Is.EqualTo(SampleCapturedAt),
                "V0 keeps CreatedAt equal to CapturedAtUtc per ADR-026 §1"
            );
            Assert.That(item.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                item.Id.Version,
                Is.EqualTo(7),
                "ADR-026 §B2': identifier must be a UUIDv7 generated via Guid.CreateVersion7"
            );
        });
    }

    [Test]
    public void Capture_AcceptsNullSource()
    {
        var item = InboxItem.Capture("body", source: null, SampleCapturedAt);

        Assert.That(item.Source, Is.Null);
    }

    [Test]
    public void Capture_RaisesExactlyOneInboxItemCapturedEvent()
    {
        var item = InboxItem.Capture("body", "chat", SampleCapturedAt);

        Assert.That(item.DomainEvents, Has.Count.EqualTo(1));
        var raised = item.DomainEvents[0];
        Assert.That(raised, Is.TypeOf<InboxItemCaptured>());
        var typed = (InboxItemCaptured)raised;
        Assert.Multiple(() =>
        {
            Assert.That(typed.InboxItemId, Is.EqualTo(item.Id));
            Assert.That(typed.CapturedAtUtc, Is.EqualTo(SampleCapturedAt));
            Assert.That(typed.OccurredOn, Is.EqualTo(SampleCapturedAt));
        });
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n  ")]
    public void Capture_ThrowsOnWhitespaceContent(string blank)
    {
        Assert.That(
            () => InboxItem.Capture(blank, source: null, SampleCapturedAt),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("content")
        );
    }

    [Test]
    public void Capture_ThrowsOnNullContent()
    {
        Assert.That(
            () => InboxItem.Capture(null!, source: null, SampleCapturedAt),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("content")
        );
    }

    [Test]
    public void Capture_ThrowsWhenContentExceedsMaxLength()
    {
        var oversize = new string('a', InboxItem.MaxContentLength + 1);

        Assert.That(
            () => InboxItem.Capture(oversize, source: null, SampleCapturedAt),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("content")
        );
    }

    [Test]
    public void Capture_AcceptsContentAtExactlyMaxLength()
    {
        var atLimit = new string('a', InboxItem.MaxContentLength);

        Assert.That(
            () => InboxItem.Capture(atLimit, source: null, SampleCapturedAt),
            Throws.Nothing
        );
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Capture_ThrowsWhenProvidedSourceIsWhitespace(string blank)
    {
        Assert.That(
            () => InboxItem.Capture("body", blank, SampleCapturedAt),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("source")
        );
    }

    [Test]
    public void Capture_ThrowsWhenSourceExceedsMaxLength()
    {
        var oversize = new string('s', InboxItem.MaxSourceLength + 1);

        Assert.That(
            () => InboxItem.Capture("body", oversize, SampleCapturedAt),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("source")
        );
    }

    [Test]
    public void Capture_ThrowsWhenCapturedAtIsNotUtc()
    {
        var notUtc = new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.That(
            () => InboxItem.Capture("body", null, notUtc),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("capturedAtUtc")
        );
    }

    [Test]
    public void Rehydrate_PopulatesAggregateWithoutRaisingEvents()
    {
        var id = Guid.CreateVersion7(SampleCapturedAt);

        var item = InboxItem.Rehydrate(
            id,
            content: "persisted",
            source: "clipboard",
            capturedAtUtc: SampleCapturedAt,
            createdAt: SampleCapturedAt
        );

        Assert.Multiple(() =>
        {
            Assert.That(item.Id, Is.EqualTo(id));
            Assert.That(item.Content, Is.EqualTo("persisted"));
            Assert.That(item.Source, Is.EqualTo("clipboard"));
            Assert.That(item.CapturedAtUtc, Is.EqualTo(SampleCapturedAt));
            Assert.That(item.CreatedAt, Is.EqualTo(SampleCapturedAt));
            Assert.That(
                item.DomainEvents,
                Is.Empty,
                "ADR-022: rehydration is not a business action and must not raise events"
            );
        });
    }

    [Test]
    public void Rehydrate_ThrowsOnEmptyId()
    {
        Assert.That(
            () =>
                InboxItem.Rehydrate(
                    Guid.Empty,
                    content: "x",
                    source: null,
                    capturedAtUtc: SampleCapturedAt,
                    createdAt: SampleCapturedAt
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("id")
        );
    }

    [Test]
    public void ClearDomainEvents_DrainsTheRaisedQueue()
    {
        var item = InboxItem.Capture("body", null, SampleCapturedAt);
        Assert.That(item.DomainEvents, Has.Count.EqualTo(1));

        item.ClearDomainEvents();

        Assert.That(item.DomainEvents, Is.Empty);
    }
}
