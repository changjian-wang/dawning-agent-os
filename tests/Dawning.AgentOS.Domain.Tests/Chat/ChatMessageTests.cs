using Dawning.AgentOS.Domain.Chat;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Tests.Chat;

/// <summary>
/// Tests for the <see cref="ChatMessage"/> entity. Per ADR-032 §决策 D1
/// only user / assistant turns are persisted (no system role); content
/// is non-whitespace and ≤ <see cref="ChatMessage.MaxContentLength"/>.
/// </summary>
[TestFixture]
public sealed class ChatMessageTests
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

    private static readonly Guid SampleSessionId = Guid.CreateVersion7(SampleCreatedAt);

    [Test]
    public void CreateUser_PopulatesPropertiesAndStampsUuidV7Id()
    {
        var msg = ChatMessage.CreateUser(SampleSessionId, "你好", SampleCreatedAt);

        Assert.Multiple(() =>
        {
            Assert.That(msg.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(msg.Id.Version, Is.EqualTo(7));
            Assert.That(msg.SessionId, Is.EqualTo(SampleSessionId));
            Assert.That(msg.Role, Is.EqualTo(ChatRole.User));
            Assert.That(msg.Content, Is.EqualTo("你好"));
            Assert.That(msg.CreatedAt, Is.EqualTo(SampleCreatedAt));
            Assert.That(msg.Model, Is.Null);
            Assert.That(msg.PromptTokens, Is.Null);
            Assert.That(msg.CompletionTokens, Is.Null);
        });
    }

    [Test]
    public void CreateAssistant_PopulatesAllFields()
    {
        var msg = ChatMessage.CreateAssistant(
            SampleSessionId,
            content: "你好，主人。",
            model: "gpt-4.1-2025-04-14",
            promptTokens: 42,
            completionTokens: 8,
            createdAtUtc: SampleCreatedAt
        );

        Assert.Multiple(() =>
        {
            Assert.That(msg.Role, Is.EqualTo(ChatRole.Assistant));
            Assert.That(msg.Content, Is.EqualTo("你好，主人。"));
            Assert.That(msg.Model, Is.EqualTo("gpt-4.1-2025-04-14"));
            Assert.That(msg.PromptTokens, Is.EqualTo(42));
            Assert.That(msg.CompletionTokens, Is.EqualTo(8));
        });
    }

    [Test]
    public void CreateAssistant_AllowsNullTokenCounts()
    {
        var msg = ChatMessage.CreateAssistant(
            SampleSessionId,
            content: "ok",
            model: "gpt-4.1",
            promptTokens: null,
            completionTokens: null,
            createdAtUtc: SampleCreatedAt
        );

        Assert.Multiple(() =>
        {
            Assert.That(msg.PromptTokens, Is.Null);
            Assert.That(msg.CompletionTokens, Is.Null);
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CreateUser_ThrowsOnBlankContent(string blank)
    {
        Assert.That(
            () => ChatMessage.CreateUser(SampleSessionId, blank, SampleCreatedAt),
            Throws.ArgumentException
        );
    }

    [Test]
    public void CreateUser_ThrowsWhenContentExceedsMaxLength()
    {
        var tooLong = new string('x', ChatMessage.MaxContentLength + 1);

        Assert.That(
            () => ChatMessage.CreateUser(SampleSessionId, tooLong, SampleCreatedAt),
            Throws.ArgumentException
        );
    }

    [Test]
    public void CreateUser_AcceptsContentExactlyAtMaxLength()
    {
        var atLimit = new string('x', ChatMessage.MaxContentLength);

        var msg = ChatMessage.CreateUser(SampleSessionId, atLimit, SampleCreatedAt);

        Assert.That(msg.Content.Length, Is.EqualTo(ChatMessage.MaxContentLength));
    }

    [Test]
    public void CreateUser_ThrowsWhenSessionIdEmpty()
    {
        Assert.That(
            () => ChatMessage.CreateUser(Guid.Empty, "hi", SampleCreatedAt),
            Throws.ArgumentException
        );
    }

    [Test]
    public void CreateUser_ThrowsWhenInstantNotUtc()
    {
        var localish = new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.That(
            () => ChatMessage.CreateUser(SampleSessionId, "hi", localish),
            Throws.ArgumentException
        );
    }

    [Test]
    public void CreateAssistant_ThrowsWhenModelBlank()
    {
        Assert.That(
            () =>
                ChatMessage.CreateAssistant(
                    SampleSessionId,
                    "ok",
                    model: "",
                    promptTokens: 1,
                    completionTokens: 1,
                    createdAtUtc: SampleCreatedAt
                ),
            Throws.ArgumentException
        );
    }

    [Test]
    public void CreateAssistant_ThrowsWhenTokenCountsNegative()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () =>
                    ChatMessage.CreateAssistant(
                        SampleSessionId,
                        "ok",
                        model: "gpt-4.1",
                        promptTokens: -1,
                        completionTokens: 1,
                        createdAtUtc: SampleCreatedAt
                    ),
                Throws.ArgumentException
            );

            Assert.That(
                () =>
                    ChatMessage.CreateAssistant(
                        SampleSessionId,
                        "ok",
                        model: "gpt-4.1",
                        promptTokens: 1,
                        completionTokens: -1,
                        createdAtUtc: SampleCreatedAt
                    ),
                Throws.ArgumentException
            );
        });
    }

    [Test]
    public void Rehydrate_RestoresAllFields()
    {
        var id = Guid.CreateVersion7(SampleCreatedAt);

        var msg = ChatMessage.Rehydrate(
            id: id,
            sessionId: SampleSessionId,
            role: ChatRole.Assistant,
            content: "ok",
            createdAt: SampleCreatedAt,
            model: "gpt-4.1",
            promptTokens: 12,
            completionTokens: 3
        );

        Assert.Multiple(() =>
        {
            Assert.That(msg.Id, Is.EqualTo(id));
            Assert.That(msg.SessionId, Is.EqualTo(SampleSessionId));
            Assert.That(msg.Role, Is.EqualTo(ChatRole.Assistant));
            Assert.That(msg.Content, Is.EqualTo("ok"));
            Assert.That(msg.CreatedAt, Is.EqualTo(SampleCreatedAt));
            Assert.That(msg.Model, Is.EqualTo("gpt-4.1"));
            Assert.That(msg.PromptTokens, Is.EqualTo(12));
            Assert.That(msg.CompletionTokens, Is.EqualTo(3));
        });
    }

    [Test]
    public void Rehydrate_ThrowsWhenIdEmpty()
    {
        Assert.That(
            () =>
                ChatMessage.Rehydrate(
                    id: Guid.Empty,
                    sessionId: SampleSessionId,
                    role: ChatRole.User,
                    content: "ok",
                    createdAt: SampleCreatedAt,
                    model: null,
                    promptTokens: null,
                    completionTokens: null
                ),
            Throws.ArgumentException
        );
    }

    [Test]
    public void Rehydrate_ThrowsWhenSessionIdEmpty()
    {
        Assert.That(
            () =>
                ChatMessage.Rehydrate(
                    id: Guid.CreateVersion7(SampleCreatedAt),
                    sessionId: Guid.Empty,
                    role: ChatRole.User,
                    content: "ok",
                    createdAt: SampleCreatedAt,
                    model: null,
                    promptTokens: null,
                    completionTokens: null
                ),
            Throws.ArgumentException
        );
    }
}
