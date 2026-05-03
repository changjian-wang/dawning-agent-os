using Dawning.AgentOS.Infrastructure.Options;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for <see cref="LlmOptionsValidator"/>. Per ADR-028 §决策 G2
/// the validator must (1) reject a structurally-invalid
/// <c>ActiveProvider</c> at startup so DI cannot pick a phantom
/// implementation, and (2) <em>accept</em> an empty <c>ApiKey</c>
/// because the warn-but-start contract surfaces missing keys at
/// call time as <c>llm.authenticationFailed</c>, not at host start.
/// </summary>
[TestFixture]
public class LlmOptionsValidationTests
{
    [Test]
    public void Validate_WithKnownActiveProvider_Succeeds()
    {
        var sut = new LlmOptionsValidator();
        var options = new LlmOptions { ActiveProvider = LlmOptions.OpenAiProviderName };

        var result = sut.Validate(name: null, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void Validate_WithDeepSeekActiveProvider_Succeeds()
    {
        var sut = new LlmOptionsValidator();
        var options = new LlmOptions { ActiveProvider = LlmOptions.DeepSeekProviderName };

        var result = sut.Validate(name: null, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void Validate_WithAzureOpenAIActiveProvider_Succeeds()
    {
        var sut = new LlmOptionsValidator();
        var options = new LlmOptions { ActiveProvider = LlmOptions.AzureOpenAiProviderName };

        var result = sut.Validate(name: null, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void Validate_WithUnknownActiveProvider_Fails()
    {
        var sut = new LlmOptionsValidator();
        var options = new LlmOptions { ActiveProvider = "Anthropic" };

        var result = sut.Validate(name: null, options);

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Anthropic"));
    }

    [Test]
    public void Validate_WithEmptyActiveProvider_Fails()
    {
        var sut = new LlmOptionsValidator();
        var options = new LlmOptions { ActiveProvider = string.Empty };

        var result = sut.Validate(name: null, options);

        Assert.That(result.Failed, Is.True);
    }

    [Test]
    public void Validate_WithEmptyApiKey_Succeeds()
    {
        // ADR-028 §决策 G2: warn-but-start. Empty ApiKey must NOT block
        // host startup; it surfaces at call time as authenticationFailed.
        var sut = new LlmOptionsValidator();
        var options = new LlmOptions
        {
            ActiveProvider = LlmOptions.OpenAiProviderName,
            Providers = new LlmProvidersOptions
            {
                OpenAI = new LlmProviderOptions
                {
                    ApiKey = string.Empty,
                    BaseUrl = "https://api.openai.com/v1",
                    Model = "gpt-4o-mini",
                },
            },
        };

        var result = sut.Validate(name: null, options);

        Assert.That(result.Succeeded, Is.True);
    }
}
