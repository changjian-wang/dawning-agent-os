using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Inbox;

/// <summary>
/// Factory helpers for inbox-side <see cref="DomainError"/> codes used by
/// the application layer. Per ADR-030 §决策 F1 and ADR-031 §决策 F1 the
/// inbox read-side adds two non-field errors that need bespoke HTTP
/// mapping in <c>InboxEndpoints</c>:
/// <list type="bullet">
///   <item>
///     <description><c>inbox.notFound</c> → HTTP 404 (item lookup miss);</description>
///   </item>
///   <item>
///     <description><c>inbox.taggingParseFailed</c> → HTTP 422 (LLM
///     output did not yield a usable tag array).</description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Capture-time validation errors (<c>inbox.content.required</c>, etc.)
/// are emitted inline by <c>InboxAppService</c> rather than threaded
/// through this class — those are field-level errors that the existing
/// <c>ResultHttpExtensions.ToHttpResult</c> already maps correctly to
/// HTTP 400. This class collects the non-field errors that need bespoke
/// HTTP mapping.
/// </remarks>
public static class InboxErrors
{
    /// <summary>The error code returned when an inbox item lookup misses.</summary>
    public const string ItemNotFoundCode = "inbox.notFound";

    /// <summary>
    /// The error code returned when the LLM completed successfully but
    /// its output could not be parsed into a non-empty tag array, per
    /// ADR-031 §决策 D2 / §决策 F1.
    /// </summary>
    public const string TaggingParseFailedCode = "inbox.taggingParseFailed";

    /// <summary>
    /// Builds an <see cref="DomainError"/> indicating that no inbox item
    /// matches the supplied id. Per ADR-030 §决策 F1 the API layer maps
    /// this to HTTP 404 in <c>InboxEndpoints</c>'s manual error switch.
    /// </summary>
    /// <param name="itemId">The id that produced the miss; included in the message for diagnostics.</param>
    /// <returns>A non-field-level error tagged with <see cref="ItemNotFoundCode"/>.</returns>
    public static DomainError ItemNotFound(Guid itemId) =>
        new(
            Code: ItemNotFoundCode,
            Message: $"Inbox item '{itemId}' not found.",
            Field: null
        );

    /// <summary>
    /// Builds an <see cref="DomainError"/> indicating that the LLM
    /// returned content that could not be normalized into at least one
    /// valid tag. Per ADR-031 §决策 F1 the API layer maps this to
    /// HTTP 422 in <c>InboxEndpoints</c>'s manual error switch — 422 is
    /// preferred over 502 because the upstream call itself succeeded;
    /// the failure is in the produced content's schema.
    /// </summary>
    /// <param name="detail">Short, log-safe explanation of why parsing failed; included in the message for diagnostics.</param>
    /// <returns>A non-field-level error tagged with <see cref="TaggingParseFailedCode"/>.</returns>
    public static DomainError TaggingParseFailed(string detail) =>
        new(
            Code: TaggingParseFailedCode,
            Message: $"Failed to parse tags from LLM output: {detail}",
            Field: null
        );
}
