using Dawning.AgentOS.Domain.Chat;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Tests.Chat;

/// <summary>
/// Tests for the <see cref="ChatSession"/> aggregate root. Per ADR-032
/// §决策 D1 the V0 schema is identity + title + created_at + updated_at;
/// per ADR-022 invariant violations throw <see cref="ArgumentException"/>.
/// </summary>
[TestFixture]
public sealed class ChatSessionTests
{
    private static readonly DateTimeOffset SampleCreatedAt = new(
        2026,
        5,
        6,
        12,
        0,
        0,
        TimeSpan.Zero
    );

    [Test]
    public void Create_PopulatesPropertiesAndStampsUuidV7Id()
    {
        var session = ChatSession.Create(SampleCreatedAt);

        Assert.Multiple(() =>
        {
            Assert.That(session.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(session.Id.Version, Is.EqualTo(7));
            Assert.That(session.Title, Is.EqualTo(ChatSession.PlaceholderTitle));
            Assert.That(session.CreatedAt, Is.EqualTo(SampleCreatedAt));
            Assert.That(session.UpdatedAt, Is.EqualTo(SampleCreatedAt));
        });
    }

    [Test]
    public void Create_ThrowsWhenInstantNotUtc()
    {
        var localish = new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.That(() => ChatSession.Create(localish), Throws.ArgumentException);
    }

    [Test]
    public void SetTitleFromFirstMessage_TruncatesToMaxTitleLength()
    {
        var session = ChatSession.Create(SampleCreatedAt);
        var longText = new string('x', ChatSession.MaxTitleLength + 10);
        var nowUtc = SampleCreatedAt.AddSeconds(1);

        session.SetTitleFromFirstMessage(longText, nowUtc);

        Assert.Multiple(() =>
        {
            Assert.That(session.Title.Length, Is.EqualTo(ChatSession.MaxTitleLength));
            Assert.That(session.Title, Is.EqualTo(new string('x', ChatSession.MaxTitleLength)));
            Assert.That(session.UpdatedAt, Is.EqualTo(nowUtc));
        });
    }

    [Test]
    public void SetTitleFromFirstMessage_TrimsLeadingAndTrailingWhitespace()
    {
        var session = ChatSession.Create(SampleCreatedAt);

        session.SetTitleFromFirstMessage("   hello world   ", SampleCreatedAt.AddSeconds(1));

        Assert.That(session.Title, Is.EqualTo("hello world"));
    }

    [Test]
    public void SetTitleFromFirstMessage_OnlyFirstCallWins()
    {
        var session = ChatSession.Create(SampleCreatedAt);
        session.SetTitleFromFirstMessage("first", SampleCreatedAt.AddSeconds(1));
        var titleAfterFirst = session.Title;
        var updatedAtAfterFirst = session.UpdatedAt;

        session.SetTitleFromFirstMessage("second", SampleCreatedAt.AddSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(session.Title, Is.EqualTo(titleAfterFirst));
            Assert.That(
                session.UpdatedAt,
                Is.EqualTo(updatedAtAfterFirst),
                "Subsequent SetTitle calls must not bump UpdatedAt."
            );
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void SetTitleFromFirstMessage_ThrowsOnBlankInput(string blank)
    {
        var session = ChatSession.Create(SampleCreatedAt);

        Assert.That(
            () => session.SetTitleFromFirstMessage(blank, SampleCreatedAt.AddSeconds(1)),
            Throws.ArgumentException
        );
    }

    [Test]
    public void Touch_UpdatesUpdatedAtOnly()
    {
        var session = ChatSession.Create(SampleCreatedAt);
        var later = SampleCreatedAt.AddMinutes(5);

        session.Touch(later);

        Assert.Multiple(() =>
        {
            Assert.That(session.UpdatedAt, Is.EqualTo(later));
            Assert.That(session.CreatedAt, Is.EqualTo(SampleCreatedAt));
            Assert.That(session.Title, Is.EqualTo(ChatSession.PlaceholderTitle));
        });
    }

    [Test]
    public void Touch_ThrowsWhenInstantNotUtc()
    {
        var session = ChatSession.Create(SampleCreatedAt);
        var localish = new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.That(() => session.Touch(localish), Throws.ArgumentException);
    }

    [Test]
    public void Rehydrate_RestoresAllFieldsWithoutChangingThem()
    {
        var id = Guid.CreateVersion7(SampleCreatedAt);
        var updatedAt = SampleCreatedAt.AddMinutes(10);

        var session = ChatSession.Rehydrate(id, "已命名会话", SampleCreatedAt, updatedAt);

        Assert.Multiple(() =>
        {
            Assert.That(session.Id, Is.EqualTo(id));
            Assert.That(session.Title, Is.EqualTo("已命名会话"));
            Assert.That(session.CreatedAt, Is.EqualTo(SampleCreatedAt));
            Assert.That(session.UpdatedAt, Is.EqualTo(updatedAt));
        });
    }

    [Test]
    public void Rehydrate_ThrowsWhenIdEmpty()
    {
        Assert.That(
            () => ChatSession.Rehydrate(Guid.Empty, "t", SampleCreatedAt, SampleCreatedAt),
            Throws.ArgumentException
        );
    }
}
