using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Chat;

/// <summary>
/// Factory helpers for chat-side <see cref="DomainError"/> codes used
/// by the application layer. Per ADR-032 §决策 K1 the V0 chat surface
/// adds one non-field error that needs bespoke HTTP mapping in
/// <c>ChatEndpoints</c>:
/// <list type="bullet">
///   <item>
///     <description>
///       <c>chat.sessionNotFound</c> → HTTP 404 (session lookup miss).
///     </description>
///   </item>
/// </list>
/// </summary>
public static class ChatErrors
{
    /// <summary>The error code returned when a chat-session lookup misses.</summary>
    public const string SessionNotFoundCode = "chat.sessionNotFound";

    /// <summary>
    /// Builds a <see cref="DomainError"/> indicating that no chat session
    /// matches the supplied id. Per ADR-032 §决策 K1 the API layer maps
    /// this to HTTP 404 in the manual error switch in
    /// <c>ChatEndpoints</c>.
    /// </summary>
    /// <param name="sessionId">The id that produced the miss; included in the message for diagnostics.</param>
    /// <returns>A non-field-level error tagged with <see cref="SessionNotFoundCode"/>.</returns>
    public static DomainError SessionNotFound(Guid sessionId) =>
        new(
            Code: SessionNotFoundCode,
            Message: $"Chat session '{sessionId}' not found.",
            Field: null
        );
}
