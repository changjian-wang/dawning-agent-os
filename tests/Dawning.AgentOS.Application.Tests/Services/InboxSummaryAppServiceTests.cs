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
/// Unit tests for <see cref="InboxSummaryAppService"/>. Per ADR-030 the
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
public sealed class InboxSummaryAppServiceTests
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

    [Test]
    public async Task SummarizeAsync_ReturnsSuccess_WhenLlmCompletes()
    {
        var item = InboxItem.Capture("用户分享了一篇文章", "chat", SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var llm = new Mock<ILlmProvider>();
        llm.SetupGet(p => p.ProviderName).Returns("OpenAI");
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LlmCompletion>.Success(
                    new LlmCompletion(
                        Content: "用户分享一篇文章。",
                        Model: "gpt-4.1",
                        PromptTokens: 42,
                        CompletionTokens: 8,
                        Latency: TimeSpan.FromMilliseconds(800)
                    )
                )
            );

        var sut = new InboxSummaryAppService(repo.Object, llm.Object);

        var result = await sut.SummarizeAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.ItemId, Is.EqualTo(item.Id));
            Assert.That(result.Value.Summary, Is.EqualTo("用户分享一篇文章。"));
            Assert.That(result.Value.Model, Is.EqualTo("gpt-4.1"));
            Assert.That(result.Value.PromptTokens, Is.EqualTo(42));
            Assert.That(result.Value.CompletionTokens, Is.EqualTo(8));
            Assert.That(result.Value.Latency, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task SummarizeAsync_ReturnsInboxNotFound_WhenItemMissing()
    {
        var missingId = Guid.CreateVersion7(SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InboxItem?)null);

        var llm = new Mock<ILlmProvider>(MockBehavior.Strict);

        var sut = new InboxSummaryAppService(repo.Object, llm.Object);

        var result = await sut.SummarizeAsync(missingId, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Code, Is.EqualTo(InboxErrors.ItemNotFoundCode));
        // Strict mock asserts CompleteAsync was never called.
        llm.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SummarizeAsync_PassesContentAsUserMessage()
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
                        Content: "summary",
                        Model: "gpt-4.1",
                        PromptTokens: null,
                        CompletionTokens: null,
                        Latency: TimeSpan.Zero
                    )
                )
            );

        var sut = new InboxSummaryAppService(repo.Object, llm.Object);
        await sut.SummarizeAsync(item.Id, CancellationToken.None);

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(captured!.Messages, Has.Count.EqualTo(2));
            Assert.That(captured.Messages[0].Role, Is.EqualTo(LlmRole.System));
            Assert.That(captured.Messages[0].Content, Is.EqualTo(InboxSummaryAppService.SystemPrompt));
            Assert.That(captured.Messages[1].Role, Is.EqualTo(LlmRole.User));
            Assert.That(captured.Messages[1].Content, Is.EqualTo("the actual material"));
            Assert.That(captured.Temperature, Is.EqualTo(InboxSummaryAppService.Temperature));
            Assert.That(captured.MaxTokens, Is.EqualTo(InboxSummaryAppService.MaxTokens));
            Assert.That(captured.Model, Is.Null, "ADR-030 §D1: model defers to provider config");
        });
    }

    [Test]
    public async Task SummarizeAsync_PropagatesLlmAuthenticationFailedError()
    {
        await AssertLlmErrorPropagated(
            LlmErrors.AuthenticationFailed("nope"),
            "llm.authenticationFailed"
        );
    }

    [Test]
    public async Task SummarizeAsync_PropagatesLlmRateLimitedError()
    {
        await AssertLlmErrorPropagated(LlmErrors.RateLimited("slow down"), "llm.rateLimited");
    }

    [Test]
    public async Task SummarizeAsync_PropagatesLlmUpstreamError()
    {
        await AssertLlmErrorPropagated(
            LlmErrors.UpstreamUnavailable("bad gateway"),
            "llm.upstreamUnavailable"
        );
    }

    [Test]
    public void SummarizeAsync_PropagatesCancellation()
    {
        var item = InboxItem.Capture("x", null, SampleCapturedAt);
        var repo = new Mock<IInboxRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var llm = new Mock<ILlmProvider>();
        llm.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new InboxSummaryAppService(repo.Object, llm.Object);

        Assert.CatchAsync<OperationCanceledException>(
            () => sut.SummarizeAsync(item.Id, CancellationToken.None)
        );
    }

    [Test]
    public void Constructor_RejectsNullDependencies()
    {
        var repo = new Mock<IInboxRepository>().Object;
        var llm = new Mock<ILlmProvider>().Object;

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new InboxSummaryAppService(null!, llm));
            Assert.Throws<ArgumentNullException>(() => new InboxSummaryAppService(repo, null!));
        });
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

        var sut = new InboxSummaryAppService(repo.Object, llm.Object);

        var result = await sut.SummarizeAsync(item.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.Length.EqualTo(1));
        Assert.That(result.Errors[0].Code, Is.EqualTo(expectedCode));
    }
}
