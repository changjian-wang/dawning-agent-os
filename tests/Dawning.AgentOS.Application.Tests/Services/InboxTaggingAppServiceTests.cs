using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Application.Services;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Domain.Inbox;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InboxTaggingAppService"/>. Per ADR-031 the
/// service:
/// <list type="bullet">
///   <item>
///     <description>loads the source aggregate via
///     <see cref="IInboxRepository.GetByIdAsync"/> and returns
///     <c>inbox.notFound</c> when absent;</description>
///   </item>
///   <item>
///     <description>builds the prompt from a fixed Chinese system message
///     plus the item content as the user message;</description>
///   </item>
///   <item>
///     <description>parses LLM output as a JSON string array and runs
///     defensive normalization (trim / dedup / length filter / cap at 5);</description>
///   </item>
///   <item>
///     <description>returns <c>inbox.taggingParseFailed</c> when the
///     LLM output cannot be parsed into a non-empty list;</description>
///   </item>
///   <item>
///     <description>passes through ADR-028 §H1 LLM error codes
///     verbatim (no remapping);</description>
///   </item>
///   <item>
///     <description>propagates <see cref="OperationCanceledException"/>
///     unchanged.</description>
///   </item>
/// </list>
/// </summary>
[TestFixture]
public sealed class InboxTaggingAppServiceTests
{
    private static readonly DateTimeOffset SampleCapturedAt = new(
        2026,
        5,
        5,
        12,
        0,
        0,
        TimeSpan.Zero
    );

    [Test]
    public async Task SuggestTagsAsync_ReturnsSuccess_WhenLlmReturnsValidJson()
    {
        var item = InboxItem.Capture("一篇关于人工智能与学习方法的文章", "chat", SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var llm = new Mock<ILlmProvider>();
        llm.SetupGet(p => p.ProviderName).Returns("OpenAI");
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LlmCompletion>.Success(
                    new LlmCompletion(
                        Content: "[\"人工智能\", \"学习方法\", \"效率工具\"]",
                        Model: "gpt-4.1",
                        PromptTokens: 42,
                        CompletionTokens: 18,
                        Latency: TimeSpan.FromMilliseconds(800)
                    )
                )
            );

        var sut = new InboxTaggingAppService(repo.Object, llm.Object);

        var result = await sut.SuggestTagsAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.ItemId, Is.EqualTo(item.Id));
            Assert.That(
                result.Value.Tags,
                Is.EqualTo(new[] { "人工智能", "学习方法", "效率工具" })
            );
            Assert.That(result.Value.Model, Is.EqualTo("gpt-4.1"));
            Assert.That(result.Value.PromptTokens, Is.EqualTo(42));
            Assert.That(result.Value.CompletionTokens, Is.EqualTo(18));
            Assert.That(result.Value.Latency, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task SuggestTagsAsync_ReturnsInboxNotFound_WhenItemMissing()
    {
        var missingId = Guid.CreateVersion7(SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InboxItem?)null);

        var llm = new Mock<ILlmProvider>(MockBehavior.Strict);

        var sut = new InboxTaggingAppService(repo.Object, llm.Object);

        var result = await sut.SuggestTagsAsync(missingId, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Code, Is.EqualTo(InboxErrors.ItemNotFoundCode));
        // Strict mock asserts CompleteAsync was never called.
        llm.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SuggestTagsAsync_PassesContentAsUserMessage()
    {
        var item = InboxItem.Capture("the actual material", "chat", SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        LlmRequest? captured = null;
        var llm = new Mock<ILlmProvider>();
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(
                Result<LlmCompletion>.Success(
                    new LlmCompletion(
                        Content: "[\"标签一\"]",
                        Model: "gpt-4.1",
                        PromptTokens: null,
                        CompletionTokens: null,
                        Latency: TimeSpan.Zero
                    )
                )
            );

        var sut = new InboxTaggingAppService(repo.Object, llm.Object);
        await sut.SuggestTagsAsync(item.Id, CancellationToken.None);

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(captured!.Messages, Has.Count.EqualTo(2));
            Assert.That(captured.Messages[0].Role, Is.EqualTo(LlmRole.System));
            Assert.That(
                captured.Messages[0].Content,
                Is.EqualTo(InboxTaggingAppService.SystemPrompt)
            );
            Assert.That(captured.Messages[1].Role, Is.EqualTo(LlmRole.User));
            Assert.That(captured.Messages[1].Content, Is.EqualTo("the actual material"));
            Assert.That(captured.Temperature, Is.EqualTo(InboxTaggingAppService.Temperature));
            Assert.That(captured.MaxTokens, Is.EqualTo(InboxTaggingAppService.MaxTokens));
            Assert.That(captured.Model, Is.Null, "ADR-031 §D1: model defers to provider config");
        });
    }

    [Test]
    public async Task SuggestTagsAsync_TrimsCodeBlockMarkers()
    {
        // LLM occasionally wraps JSON in a fence even when the system
        // prompt forbids it; the AppService strips the noise per
        // ADR-031 §决策 D2 step 1.
        await AssertSuccessfulTags(
            llmContent: "```json\n[\"人工智能\", \"学习方法\"]\n```",
            expectedTags: ["人工智能", "学习方法"]
        );
    }

    [Test]
    public async Task SuggestTagsAsync_TruncatesToFiveTags()
    {
        // LLM returned 7 tags; per ADR-031 §决策 D2 step 5 the
        // AppService caps to MaxTagCount=5 preserving order.
        await AssertSuccessfulTags(
            llmContent: "[\"一标签\", \"二标签\", \"三标签\", \"四标签\", \"五标签\", \"六标签\", \"七标签\"]",
            expectedTags: ["一标签", "二标签", "三标签", "四标签", "五标签"]
        );
    }

    [Test]
    public async Task SuggestTagsAsync_DeduplicatesTags()
    {
        // Per ADR-031 §决策 D2 step 4 first-occurrence wins.
        await AssertSuccessfulTags(
            llmContent: "[\"重复词\", \"重复词\", \"另一词\"]",
            expectedTags: ["重复词", "另一词"]
        );
    }

    [Test]
    public async Task SuggestTagsAsync_FiltersOutOfRangeLengths()
    {
        // 1-char tag and 13-char tag are dropped; per ADR-031 §决策 D2
        // step 3 only MinTagLength..MaxTagLength survives.
        await AssertSuccessfulTags(
            llmContent: "[\"短\", \"正常标签\", \"这个标签超过了十二个字符的硬上限\"]",
            expectedTags: ["正常标签"]
        );
    }

    [Test]
    public async Task SuggestTagsAsync_ReturnsTaggingParseFailed_WhenJsonInvalid()
    {
        await AssertTagsParseFailedFor("this is not json at all");
    }

    [Test]
    public async Task SuggestTagsAsync_ReturnsTaggingParseFailed_WhenJsonIsObject()
    {
        // Object instead of array — Deserialize<string[]> throws.
        await AssertTagsParseFailedFor("{\"tags\": [\"x\"]}");
    }

    [Test]
    public async Task SuggestTagsAsync_ReturnsTaggingParseFailed_WhenAllTagsFiltered()
    {
        // All elements are too short or empty; normalization yields 0
        // tags; per ADR-031 §决策 D2 step 6 this is taggingParseFailed.
        await AssertTagsParseFailedFor("[\"\", \"a\", \"  \"]");
    }

    [Test]
    public async Task SuggestTagsAsync_ReturnsTaggingParseFailed_WhenEmptyArray()
    {
        await AssertTagsParseFailedFor("[]");
    }

    [Test]
    public async Task SuggestTagsAsync_PropagatesLlmAuthenticationFailedError()
    {
        await AssertLlmErrorPropagated(
            LlmErrors.AuthenticationFailed("nope"),
            "llm.authenticationFailed"
        );
    }

    [Test]
    public async Task SuggestTagsAsync_PropagatesLlmRateLimitedError()
    {
        await AssertLlmErrorPropagated(LlmErrors.RateLimited("slow down"), "llm.rateLimited");
    }

    [Test]
    public async Task SuggestTagsAsync_PropagatesLlmUpstreamError()
    {
        await AssertLlmErrorPropagated(
            LlmErrors.UpstreamUnavailable("bad gateway"),
            "llm.upstreamUnavailable"
        );
    }

    [Test]
    public void SuggestTagsAsync_PropagatesCancellation()
    {
        var item = InboxItem.Capture("x", null, SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var llm = new Mock<ILlmProvider>();
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new InboxTaggingAppService(repo.Object, llm.Object);

        Assert.CatchAsync<OperationCanceledException>(
            () => sut.SuggestTagsAsync(item.Id, CancellationToken.None)
        );
    }

    [Test]
    public void Constructor_RejectsNullDependencies()
    {
        var repo = new Mock<IInboxRepository>().Object;
        var llm = new Mock<ILlmProvider>().Object;

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new InboxTaggingAppService(null!, llm));
            Assert.Throws<ArgumentNullException>(() => new InboxTaggingAppService(repo, null!));
        });
    }

    private static async Task AssertSuccessfulTags(string llmContent, string[] expectedTags)
    {
        var item = InboxItem.Capture("hello", null, SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var llm = new Mock<ILlmProvider>();
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LlmCompletion>.Success(
                    new LlmCompletion(
                        Content: llmContent,
                        Model: "gpt-4.1",
                        PromptTokens: 10,
                        CompletionTokens: 5,
                        Latency: TimeSpan.FromMilliseconds(50)
                    )
                )
            );

        var sut = new InboxTaggingAppService(repo.Object, llm.Object);

        var result = await sut.SuggestTagsAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Tags, Is.EqualTo(expectedTags));
    }

    private static async Task AssertTagsParseFailedFor(string llmContent)
    {
        var item = InboxItem.Capture("hello", null, SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var llm = new Mock<ILlmProvider>();
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LlmCompletion>.Success(
                    new LlmCompletion(
                        Content: llmContent,
                        Model: "gpt-4.1",
                        PromptTokens: 10,
                        CompletionTokens: 5,
                        Latency: TimeSpan.FromMilliseconds(50)
                    )
                )
            );

        var sut = new InboxTaggingAppService(repo.Object, llm.Object);

        var result = await sut.SuggestTagsAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Code, Is.EqualTo(InboxErrors.TaggingParseFailedCode));
    }

    private static async Task AssertLlmErrorPropagated(DomainError llmError, string expectedCode)
    {
        var item = InboxItem.Capture("hello", null, SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var llm = new Mock<ILlmProvider>();
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmCompletion>.Failure(llmError));

        var sut = new InboxTaggingAppService(repo.Object, llm.Object);

        var result = await sut.SuggestTagsAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Code, Is.EqualTo(expectedCode));
    }
}
