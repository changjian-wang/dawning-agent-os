using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Domain.Memory;
using Microsoft.Extensions.Logging;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Per ADR-038 §决策 H1 the default <see cref="IChatMemoryRetriever"/>
/// implementation. Tokenizes the user message into keywords (ASCII word
/// runs ≥ 2 chars + sliding CJK 2-grams), forwards them to
/// <see cref="IMemoryLedgerRepository.SearchByKeywordsAsync"/>, and
/// silently degrades to an empty list on any infrastructure failure
/// (§决策 F1) so chat keeps streaming when memory retrieval breaks.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-038 §决策 A1 the tokenization rules:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       Empty / whitespace input ⇒ empty keyword list ⇒ no DB call.
///     </description>
///   </item>
///   <item>
///     <description>
///       ASCII / digit runs of <see cref="MinAsciiTokenLength"/> chars
///       or longer, lower-cased with the invariant culture so the
///       Turkish-i pitfall cannot sabotage downstream <c>LIKE</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       CJK runs (basic block U+4E00 – U+9FFF) sliced into sliding
///       <see cref="CjkNGramSize"/>-grams; per the §plan-first §Q5
///       answer single-char tokens are NOT emitted because they
///       over-match aggressively on stop-word characters
///       (e.g. "的", "一", "是").
///     </description>
///   </item>
///   <item>
///     <description>
///       Insertion-order dedup, then truncate to at most
///       <see cref="MaxKeywordCount"/> entries.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class ChatMemoryRetriever(
    IMemoryLedgerRepository memoryRepository,
    ILogger<ChatMemoryRetriever> logger
) : IChatMemoryRetriever
{
    /// <summary>
    /// Per ADR-038 §决策 C1 the cap on memory entries injected per
    /// chat call. Hardcoded — overriding this is a code change and goes
    /// through ADR review.
    /// </summary>
    public const int MaxRetrievedEntries = 5;

    /// <summary>
    /// Per ADR-038 §决策 A1 the cap on tokens fed into the repository's
    /// OR-chain. 16 is generous for typical chat sentences while
    /// preventing pathological inputs from blowing up generated SQL.
    /// </summary>
    public const int MaxKeywordCount = 16;

    /// <summary>
    /// Per ADR-038 §决策 A1 the minimum length of an ASCII / digit
    /// keyword run; single-character tokens are dropped because they
    /// over-match.
    /// </summary>
    public const int MinAsciiTokenLength = 2;

    /// <summary>
    /// Per ADR-038 §决策 A1 / §plan-first §Q5 the n-gram size for CJK
    /// runs. 2-gram balances recall (1-gram over-matches stop-words)
    /// vs precision (3-gram misses common phrasings).
    /// </summary>
    public const int CjkNGramSize = 2;

    private static readonly Regex AsciiWordRegex = new(
        @"[A-Za-z0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private readonly IMemoryLedgerRepository _memoryRepository =
        memoryRepository ?? throw new ArgumentNullException(nameof(memoryRepository));
    private readonly ILogger<ChatMemoryRetriever> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryLedgerEntry>> RetrieveAsync(
        string userMessage,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        var keywords = Tokenize(userMessage);
        if (keywords.Count == 0)
        {
            // Empty input or no extractable keywords ⇒ no SQL, no
            // injection. The repository's SearchByKeywordsAsync would
            // also short-circuit but we skip even the connection open.
            return Array.Empty<MemoryLedgerEntry>();
        }

        try
        {
            return await _memoryRepository
                .SearchByKeywordsAsync(keywords, MaxRetrievedEntries, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the user's intent — propagate so the chat
            // pipeline can stop cleanly.
            throw;
        }
        catch (Exception ex)
        {
            // Per ADR-038 §决策 F1 silent degrade: chat must continue
            // even when the memory store is broken. Warn-level log so
            // operators see it in dev / dogfood without the user being
            // notified (per §F1 vs F3 trade-off in the ADR).
            _logger.LogWarning(
                ex,
                "Chat memory retrieval failed; chat continues without injection (ADR-038 §F1)."
            );
            return Array.Empty<MemoryLedgerEntry>();
        }
    }

    /// <summary>
    /// Per ADR-038 §决策 A1 tokenizes <paramref name="userMessage"/> into
    /// the keyword list described in the class summary. Internal so unit
    /// tests in <c>Dawning.AgentOS.Application.Tests</c> can pin the
    /// edge cases listed in the ADR's §可机器化判据 directly. Always
    /// returns a non-null list; empty for null / whitespace input.
    /// </summary>
    internal static IReadOnlyList<string> Tokenize(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var keywords = new List<string>(capacity: 8);

        foreach (Match match in AsciiWordRegex.Matches(userMessage))
        {
            if (match.Length < MinAsciiTokenLength)
            {
                continue;
            }

            var token = match.Value.ToLower(CultureInfo.InvariantCulture);
            if (seen.Add(token))
            {
                keywords.Add(token);
                if (keywords.Count >= MaxKeywordCount)
                {
                    return keywords;
                }
            }
        }

        var run = new StringBuilder(capacity: 16);
        for (var i = 0; i <= userMessage.Length; i++)
        {
            var atEnd = i == userMessage.Length;
            var ch = atEnd ? '\0' : userMessage[i];
            var isCjk = !atEnd && IsCjkUnifiedIdeograph(ch);

            if (isCjk)
            {
                run.Append(ch);
                continue;
            }

            if (run.Length >= CjkNGramSize)
            {
                var slice = run.ToString();
                for (var j = 0; j <= slice.Length - CjkNGramSize; j++)
                {
                    var bigram = slice.Substring(j, CjkNGramSize);
                    if (seen.Add(bigram))
                    {
                        keywords.Add(bigram);
                        if (keywords.Count >= MaxKeywordCount)
                        {
                            return keywords;
                        }
                    }
                }
            }

            run.Clear();
        }

        return keywords;
    }

    private static bool IsCjkUnifiedIdeograph(char ch) => ch is >= '\u4E00' and <= '\u9FFF';
}
