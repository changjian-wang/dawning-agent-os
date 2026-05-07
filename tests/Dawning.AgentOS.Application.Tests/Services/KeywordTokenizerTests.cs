using Dawning.AgentOS.Application.Services;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Pins the keyword tokenization rules listed in ADR-038 §可机器化判据
/// to <see cref="ChatMemoryRetriever.Tokenize(string?)"/>. The helper is
/// internal; this test fixture sees it via the
/// <c>InternalsVisibleTo</c> attribute on
/// <c>Dawning.AgentOS.Application.csproj</c>.
/// </summary>
[TestFixture]
public sealed class KeywordTokenizerTests
{
    [Test]
    public void Tokenize_NullInput_ReturnsEmpty()
    {
        var tokens = ChatMemoryRetriever.Tokenize(null);

        Assert.That(tokens, Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n  ")]
    public void Tokenize_BlankInput_ReturnsEmpty(string input)
    {
        var tokens = ChatMemoryRetriever.Tokenize(input);

        Assert.That(tokens, Is.Empty);
    }

    [Test]
    public void Tokenize_AsciiWords_AreLowerCasedInvariant()
    {
        var tokens = ChatMemoryRetriever.Tokenize("Hello WORLD Foo");

        Assert.That(tokens, Is.EqualTo(new[] { "hello", "world", "foo" }));
    }

    [Test]
    public void Tokenize_SingleCharAsciiWords_AreDropped()
    {
        // Per ADR-038 §决策 A1 ASCII / digit runs shorter than
        // MinAsciiTokenLength (= 2) are dropped to avoid over-matching.
        var tokens = ChatMemoryRetriever.Tokenize("a I x but bug");

        Assert.That(tokens, Is.EqualTo(new[] { "but", "bug" }));
    }

    [Test]
    public void Tokenize_DigitsArePreserved()
    {
        var tokens = ChatMemoryRetriever.Tokenize("v2 alpha 42 release-2026");

        Assert.That(tokens, Is.EqualTo(new[] { "v2", "alpha", "42", "release", "2026" }));
    }

    [Test]
    public void Tokenize_DuplicateAsciiWords_AreDeduped_OrderPreserved()
    {
        var tokens = ChatMemoryRetriever.Tokenize("foo bar foo BAR Foo");

        Assert.That(tokens, Is.EqualTo(new[] { "foo", "bar" }));
    }

    [Test]
    public void Tokenize_CjkRun_IsSlicedIntoSlidingBigrams()
    {
        var tokens = ChatMemoryRetriever.Tokenize("我喜欢吃苹果");

        // Sliding 2-grams over the 6 CJK chars produce 5 distinct bigrams.
        Assert.That(
            tokens,
            Is.EqualTo(new[] { "我喜", "喜欢", "欢吃", "吃苹", "苹果" })
        );
    }

    [Test]
    public void Tokenize_SingleCjkChar_IsDropped()
    {
        // Per ADR-038 §可机器化判据 single-char CJK tokens are not
        // emitted because they over-match on stop-word characters.
        var tokens = ChatMemoryRetriever.Tokenize("好");

        Assert.That(tokens, Is.Empty);
    }

    [Test]
    public void Tokenize_DuplicateCjkBigrams_AreDeduped()
    {
        // "苹果苹果" produces ["苹果", "果苹", "苹果"] before dedup.
        var tokens = ChatMemoryRetriever.Tokenize("苹果苹果");

        Assert.That(tokens, Is.EqualTo(new[] { "苹果", "果苹" }));
    }

    [Test]
    public void Tokenize_MixedAsciiAndCjk_AreEmittedSeparately()
    {
        var tokens = ChatMemoryRetriever.Tokenize("Buy 苹果 today");

        Assert.That(tokens, Is.EqualTo(new[] { "buy", "today", "苹果" }));
    }

    [Test]
    public void Tokenize_NonCjkSymbolsBetweenCjkRuns_BreakTheRun()
    {
        // "苹果，香蕉" ⇒ run1 = "苹果", run2 = "香蕉"; bigrams from each.
        var tokens = ChatMemoryRetriever.Tokenize("苹果，香蕉");

        Assert.That(tokens, Is.EqualTo(new[] { "苹果", "香蕉" }));
    }

    [Test]
    public void Tokenize_CapsAtMaxKeywordCount_OnPathologicalInput()
    {
        // Build a string with > MaxKeywordCount distinct ASCII words.
        var distinct = Enumerable.Range(1, ChatMemoryRetriever.MaxKeywordCount + 5)
            .Select(i => $"w{i:D3}")
            .ToArray();
        var input = string.Join(' ', distinct);

        var tokens = ChatMemoryRetriever.Tokenize(input);

        Assert.That(tokens, Has.Count.EqualTo(ChatMemoryRetriever.MaxKeywordCount));
        Assert.That(tokens[0], Is.EqualTo("w001"));
    }

    [Test]
    public void Tokenize_TurkishIPitfall_AsciiLowerCaseUsesInvariantCulture()
    {
        // Per ADR-038 §决策 A1 the lower-case must be invariant so that
        // a Turkish-locale runtime cannot map 'I' to a dotless ı, which
        // would break downstream LIKE matching.
        var tokens = ChatMemoryRetriever.Tokenize("INDEX index");

        Assert.That(tokens, Is.EqualTo(new[] { "index" }));
    }
}
