using Dawning.AgentOS.Abstractions;
using Dawning.AgentOS.Abstractions.Llm;
using Dawning.AgentOS.Application.Chat;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Services;
using Dawning.AgentOS.Domain.Chat;
using Dawning.AgentOS.Domain.Memory;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ChatAppService"/>. Per ADR-032 §决策 J1 the
/// service:
/// <list type="bullet">
///   <item>
///     <description>validates content (required, length cap) and surfaces
///     failures as <c>Result.Failure</c> with field-level entries that
///     map to HTTP 400;</description>
///   </item>
///   <item>
///     <description>returns <c>chat.sessionNotFound</c> for unknown ids
///     (HTTP 404);</description>
///   </item>
///   <item>
///     <description>persists the user turn before the LLM call so a
///     mid-stream failure leaves a recoverable trail;</description>
///   </item>
///   <item>
///     <description>derives the title from the first user turn;</description>
///   </item>
///   <item>
///     <description>persists the assistant turn only after a clean
///     <see cref="LlmStreamChunkKind.Done"/> chunk arrives.</description>
///   </item>
/// </list>
/// </summary>
[TestFixture]
public sealed class ChatAppServiceTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task CreateSessionAsync_PersistsAndReturnsDtoWithClockTimestamps()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        ChatSession? captured = null;
        var repo = new Mock<IChatSessionRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback<ChatSession, CancellationToken>((session, _) => captured = session)
            .Returns(Task.CompletedTask);
        var llm = new Mock<ILlmProvider>(MockBehavior.Strict);
        var retriever = new Mock<IChatMemoryRetriever>(MockBehavior.Strict);

        var sut = new ChatAppService(clock.Object, repo.Object, llm.Object, retriever.Object);

        var result = await sut.CreateSessionAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Id, Is.EqualTo(captured!.Id));
            Assert.That(result.Value.Title, Is.EqualTo(ChatSession.PlaceholderTitle));
            Assert.That(result.Value.CreatedAt, Is.EqualTo(SampleNow));
            Assert.That(result.Value.UpdatedAt, Is.EqualTo(SampleNow));
        });
        repo.Verify(
            r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(201)]
    public async Task ListSessionsAsync_ReturnsFieldFailure_WhenLimitOutOfRange(int limit)
    {
        var sut = BuildSut(out _, out _, out _);

        var result = await sut.ListSessionsAsync(
            new ChatSessionListQuery(limit, 0),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Code, Is.EqualTo("chat.limit.outOfRange"));
        Assert.That(result.Errors[0].Field, Is.EqualTo("limit"));
    }

    [Test]
    public async Task ListSessionsAsync_ReturnsFieldFailure_WhenOffsetNegative()
    {
        var sut = BuildSut(out _, out _, out _);

        var result = await sut.ListSessionsAsync(
            new ChatSessionListQuery(20, -1),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("chat.offset.outOfRange"));
        Assert.That(result.Errors[0].Field, Is.EqualTo("offset"));
    }

    [Test]
    public async Task ListSessionsAsync_ReturnsMappedDtos_OnSuccess()
    {
        var sut = BuildSut(out _, out var repo, out _);
        var session1 = ChatSession.Create(SampleNow);
        var session2 = ChatSession.Create(SampleNow.AddMinutes(1));
        repo.Setup(r => r.ListAsync(20, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([session1, session2]);

        var result = await sut.ListSessionsAsync(
            new ChatSessionListQuery(20, 0),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Items, Has.Length.EqualTo(2));
            Assert.That(result.Value.Limit, Is.EqualTo(20));
            Assert.That(result.Value.Offset, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ListMessagesAsync_ReturnsSessionNotFound_ForUnknownSession()
    {
        var sut = BuildSut(out _, out var repo, out _);
        var unknownId = Guid.CreateVersion7(SampleNow);
        repo.Setup(r => r.GetAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);

        var result = await sut.ListMessagesAsync(unknownId, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo(ChatErrors.SessionNotFoundCode));
    }

    [Test]
    public async Task ListMessagesAsync_ReturnsMappedDtos_WhenSessionExists()
    {
        var sut = BuildSut(out _, out var repo, out _);
        var session = ChatSession.Create(SampleNow);
        repo.Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.LoadMessagesAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ChatMessage.CreateUser(session.Id, "hi", SampleNow),
                ChatMessage.CreateAssistant(
                    session.Id,
                    "hello",
                    "gpt-4.1",
                    promptTokens: 1,
                    completionTokens: 1,
                    createdAtUtc: SampleNow.AddSeconds(1)
                ),
            ]);

        var result = await sut.ListMessagesAsync(session.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Has.Count.EqualTo(2));
            Assert.That(result.Value[0].Role, Is.EqualTo("user"));
            Assert.That(result.Value[1].Role, Is.EqualTo("assistant"));
            Assert.That(result.Value[1].Model, Is.EqualTo("gpt-4.1"));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task SendMessageStreamAsync_ReturnsContentRequired_OnBlank(string blank)
    {
        var sut = BuildSut(out _, out _, out _);

        var result = await sut.SendMessageStreamAsync(
            Guid.CreateVersion7(SampleNow),
            new SendMessageRequest(blank),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("chat.content.required"));
    }

    [Test]
    public async Task SendMessageStreamAsync_ReturnsContentTooLong_WhenAboveLimit()
    {
        var sut = BuildSut(out _, out _, out _);
        var oversize = new string('x', ChatMessage.MaxContentLength + 1);

        var result = await sut.SendMessageStreamAsync(
            Guid.CreateVersion7(SampleNow),
            new SendMessageRequest(oversize),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("chat.content.tooLong"));
    }

    [Test]
    public async Task SendMessageStreamAsync_ReturnsSessionNotFound_ForUnknownSession()
    {
        var sut = BuildSut(out _, out var repo, out _);
        var unknownId = Guid.CreateVersion7(SampleNow);
        repo.Setup(r => r.GetAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);

        var result = await sut.SendMessageStreamAsync(
            unknownId,
            new SendMessageRequest("hi"),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo(ChatErrors.SessionNotFoundCode));
    }

    [Test]
    public async Task SendMessageStreamAsync_PersistsUserBeforeStream_AndDerivesTitleOnFirstTurn()
    {
        var sut = BuildSut(out var clock, out var repo, out var llm);
        var session = ChatSession.Create(SampleNow.AddMinutes(-5));
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repo.Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.LoadMessagesAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatMessage>());

        var addedMessages = new List<ChatMessage>();
        repo.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChatMessage, CancellationToken>((m, _) => addedMessages.Add(m))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Streaming reply: a single delta + a clean done.
        llm.Setup(l => l.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                BuildStream(
                    LlmStreamChunk.ForDelta("hello"),
                    LlmStreamChunk.ForDone(
                        "gpt-4.1",
                        promptTokens: 5,
                        completionTokens: 1,
                        latency: TimeSpan.FromMilliseconds(100)
                    )
                )
            );

        var result = await sut.SendMessageStreamAsync(
            session.Id,
            new SendMessageRequest("你好世界"),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);

        // Drain the async stream; that triggers the persistence side effects.
        var emitted = new List<LlmStreamChunk>();
        await foreach (var chunk in result.Value)
        {
            emitted.Add(chunk);
        }

        Assert.Multiple(() =>
        {
            Assert.That(emitted, Has.Count.EqualTo(2));
            // User turn must be persisted before assistant turn.
            Assert.That(addedMessages, Has.Count.EqualTo(2));
            Assert.That(addedMessages[0].Role, Is.EqualTo(ChatRole.User));
            Assert.That(addedMessages[0].Content, Is.EqualTo("你好世界"));
            Assert.That(addedMessages[1].Role, Is.EqualTo(ChatRole.Assistant));
            Assert.That(addedMessages[1].Content, Is.EqualTo("hello"));
            Assert.That(addedMessages[1].Model, Is.EqualTo("gpt-4.1"));
            Assert.That(addedMessages[1].PromptTokens, Is.EqualTo(5));
            Assert.That(addedMessages[1].CompletionTokens, Is.EqualTo(1));
            // Title derived from first user turn (≤ MaxTitleLength so unchanged).
            Assert.That(session.Title, Is.EqualTo("你好世界"));
        });
    }

    [Test]
    public async Task SendMessageStreamAsync_SkipsAssistantPersistence_OnError()
    {
        var sut = BuildSut(out var clock, out var repo, out var llm);
        var session = ChatSession.Create(SampleNow);
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repo.Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.LoadMessagesAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatMessage>());
        repo.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        llm.Setup(l => l.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                BuildStream(
                    LlmStreamChunk.ForDelta("partial"),
                    LlmStreamChunk.ForError(LlmErrors.UpstreamUnavailable("boom"))
                )
            );

        var result = await sut.SendMessageStreamAsync(
            session.Id,
            new SendMessageRequest("test"),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);

        var emitted = new List<LlmStreamChunk>();
        await foreach (var chunk in result.Value)
        {
            emitted.Add(chunk);
        }

        // Only the user turn must be persisted (1 AddMessage call).
        repo.Verify(
            r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[1].Kind, Is.EqualTo(LlmStreamChunkKind.Error));
    }

    [Test]
    public async Task SendMessageStreamAsync_TouchesSessionInsteadOfRetitling_OnSubsequentTurn()
    {
        var sut = BuildSut(out var clock, out var repo, out var llm);
        var session = ChatSession.Create(SampleNow.AddMinutes(-10));
        // Pretend the title was already set on a previous turn.
        session.SetTitleFromFirstMessage("已有标题", SampleNow.AddMinutes(-9));
        var titleBefore = session.Title;

        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repo.Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.LoadMessagesAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ChatMessage.CreateUser(session.Id, "earlier", SampleNow.AddMinutes(-9)),
            ]);
        repo.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        llm.Setup(l => l.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                BuildStream(
                    LlmStreamChunk.ForDone(
                        "gpt-4.1",
                        promptTokens: 1,
                        completionTokens: 0,
                        latency: TimeSpan.Zero
                    )
                )
            );

        var result = await sut.SendMessageStreamAsync(
            session.Id,
            new SendMessageRequest("a follow-up"),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        await foreach (var _ in result.Value) { }

        Assert.Multiple(() =>
        {
            Assert.That(
                session.Title,
                Is.EqualTo(titleBefore),
                "Title must not change on follow-up turns."
            );
            Assert.That(session.UpdatedAt, Is.EqualTo(SampleNow), "Touch must bump UpdatedAt.");
        });
    }

    private static ChatAppService BuildSut(
        out Mock<IClock> clock,
        out Mock<IChatSessionRepository> repository,
        out Mock<ILlmProvider> llm
    )
    {
        clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repository = new Mock<IChatSessionRepository>();
        llm = new Mock<ILlmProvider>();
        var retriever = new Mock<IChatMemoryRetriever>();
        // Default — no memory cited — keeps existing tests behavior
        // unchanged. Tests that exercise injection use the dedicated
        // BuildSutWithRetriever overload.
        retriever
            .Setup(r => r.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemoryLedgerEntry>());
        return new ChatAppService(clock.Object, repository.Object, llm.Object, retriever.Object);
    }

    [Test]
    public async Task SendMessageStreamAsync_EmitsMemoryAnnotationChunkFirst_WhenMemoriesRetrieved()
    {
        var sut = BuildSutWithRetriever(
            out var clock,
            out var repo,
            out var llm,
            out var retriever
        );
        var session = ChatSession.Create(SampleNow);
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repo.Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.LoadMessagesAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatMessage>());
        repo.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var memory1 = NewMemory("我喜欢吃苹果");
        var memory2 = NewMemory(new string('x', 100));
        retriever
            .Setup(r => r.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([memory1, memory2]);

        llm.Setup(l => l.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                BuildStream(
                    LlmStreamChunk.ForDelta("ok"),
                    LlmStreamChunk.ForDone(
                        "gpt-4.1",
                        promptTokens: 1,
                        completionTokens: 1,
                        latency: TimeSpan.Zero
                    )
                )
            );

        var result = await sut.SendMessageStreamAsync(
            session.Id,
            new SendMessageRequest("我喜欢苹果"),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);

        var emitted = new List<LlmStreamChunk>();
        await foreach (var chunk in result.Value)
        {
            emitted.Add(chunk);
        }

        Assert.Multiple(() =>
        {
            // MemoryAnnotation must be the FIRST chunk (ADR-038 §决策 D2).
            Assert.That(emitted[0].Kind, Is.EqualTo(LlmStreamChunkKind.MemoryAnnotation));
            Assert.That(emitted[0].MemoryAnnotations, Is.Not.Null);
            Assert.That(emitted[0].MemoryAnnotations!, Has.Count.EqualTo(2));
            Assert.That(emitted[0].MemoryAnnotations![0].Id, Is.EqualTo(memory1.Id));
            Assert.That(emitted[0].MemoryAnnotations![0].ContentPreview, Is.EqualTo("我喜欢吃苹果"));
            Assert.That(emitted[0].MemoryAnnotations![1].Id, Is.EqualTo(memory2.Id));
            // 100 chars truncated to 80 + ellipsis.
            Assert.That(
                emitted[0].MemoryAnnotations![1].ContentPreview,
                Is.EqualTo(new string('x', 80) + "…")
            );
            Assert.That(emitted[1].Kind, Is.EqualTo(LlmStreamChunkKind.Delta));
            Assert.That(emitted[2].Kind, Is.EqualTo(LlmStreamChunkKind.Done));
        });
    }

    [Test]
    public async Task SendMessageStreamAsync_DoesNotEmitMemoryAnnotation_WhenNoMemoriesRetrieved()
    {
        var sut = BuildSutWithRetriever(
            out var clock,
            out var repo,
            out var llm,
            out var retriever
        );
        var session = ChatSession.Create(SampleNow);
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repo.Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.LoadMessagesAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatMessage>());
        repo.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        retriever
            .Setup(r => r.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemoryLedgerEntry>());

        llm.Setup(l => l.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                BuildStream(
                    LlmStreamChunk.ForDelta("hi"),
                    LlmStreamChunk.ForDone(
                        "gpt-4.1",
                        promptTokens: 1,
                        completionTokens: 1,
                        latency: TimeSpan.Zero
                    )
                )
            );

        var result = await sut.SendMessageStreamAsync(
            session.Id,
            new SendMessageRequest("hi"),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);

        var emitted = new List<LlmStreamChunk>();
        await foreach (var chunk in result.Value)
        {
            emitted.Add(chunk);
        }

        Assert.That(
            emitted.Any(c => c.Kind == LlmStreamChunkKind.MemoryAnnotation),
            Is.False,
            "Empty memory list ⇒ no MemoryAnnotation chunk per ADR-038 §决策 D2."
        );
    }

    [Test]
    public async Task SendMessageStreamAsync_AppendsMemoryTailToSystemPrompt_WhenMemoriesRetrieved()
    {
        var sut = BuildSutWithRetriever(
            out var clock,
            out var repo,
            out var llm,
            out var retriever
        );
        var session = ChatSession.Create(SampleNow);
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repo.Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.LoadMessagesAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatMessage>());
        repo.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var memoryEntry = NewMemory("用户喜欢清淡饮食");
        retriever
            .Setup(r => r.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([memoryEntry]);

        LlmRequest? capturedRequest = null;
        llm.Setup(l => l.CompleteStreamAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(
                BuildStream(
                    LlmStreamChunk.ForDone(
                        "gpt-4.1",
                        promptTokens: 1,
                        completionTokens: 0,
                        latency: TimeSpan.Zero
                    )
                )
            );

        var result = await sut.SendMessageStreamAsync(
            session.Id,
            new SendMessageRequest("饮食建议"),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        await foreach (var _ in result.Value) { }

        Assert.That(capturedRequest, Is.Not.Null);
        var systemMessages = capturedRequest!
            .Messages.Where(m => m.Role == LlmRole.System)
            .ToList();
        Assert.Multiple(() =>
        {
            // Per ADR-038 §决策 B1 a single system message carries the
            // memory tail, not a separate role=system entry.
            Assert.That(systemMessages, Has.Count.EqualTo(1));
            Assert.That(systemMessages[0].Content, Does.StartWith(ChatAppService.SystemPrompt));
            Assert.That(
                systemMessages[0].Content,
                Does.Contain(ChatAppService.MemorySectionHeader)
            );
            Assert.That(systemMessages[0].Content, Does.Contain("用户喜欢清淡饮食"));
        });
    }

    [Test]
    public void BuildSystemContent_NoMemories_ReturnsBaseSystemPromptUnchanged()
    {
        // Per ADR-038 §决策 B1 absence of memories must yield the same
        // single system message shape as a pre-ADR-038 chat call so
        // existing provider plumbing keeps working.
        var content = ChatAppService.BuildSystemContent(Array.Empty<MemoryLedgerEntry>());

        Assert.That(content, Is.EqualTo(ChatAppService.SystemPrompt));
    }

    [Test]
    public void BuildAnnotations_TruncatesContentPreview_AtConfiguredLength()
    {
        var shortEntry = NewMemory("短记忆");
        var longContent = new string('a', ChatAppService.MemoryPreviewMaxLength + 50);
        var longEntry = NewMemory(longContent);

        var items = ChatAppService.BuildAnnotations([shortEntry, longEntry]);

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(items[0].Id, Is.EqualTo(shortEntry.Id));
            Assert.That(items[0].ContentPreview, Is.EqualTo("短记忆"));
            Assert.That(items[1].Id, Is.EqualTo(longEntry.Id));
            Assert.That(
                items[1].ContentPreview,
                Is.EqualTo(
                    new string('a', ChatAppService.MemoryPreviewMaxLength)
                        + ChatAppService.PreviewEllipsis
                )
            );
        });
    }

    private static MemoryLedgerEntry NewMemory(string content) =>
        MemoryLedgerEntry.Create(
            content: content,
            scope: null,
            source: MemorySource.UserExplicit,
            isExplicit: true,
            confidence: 1.0,
            sensitivity: MemorySensitivity.Normal,
            createdAtUtc: SampleNow
        );

    private static ChatAppService BuildSutWithRetriever(
        out Mock<IClock> clock,
        out Mock<IChatSessionRepository> repository,
        out Mock<ILlmProvider> llm,
        out Mock<IChatMemoryRetriever> retriever
    )
    {
        clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);
        repository = new Mock<IChatSessionRepository>();
        llm = new Mock<ILlmProvider>();
        retriever = new Mock<IChatMemoryRetriever>(MockBehavior.Strict);
        return new ChatAppService(clock.Object, repository.Object, llm.Object, retriever.Object);
    }

    private static async IAsyncEnumerable<LlmStreamChunk> BuildStream(
        params LlmStreamChunk[] chunks
    )
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
        }

        await Task.CompletedTask;
    }
}
