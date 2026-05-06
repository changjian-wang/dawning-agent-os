using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Llm;

/// <summary>
/// Stable <see cref="DomainError"/> factory for <see cref="ILlmProvider"/>
/// failures. Per ADR-028 §决策 H1 every business-level LLM failure flows
/// through these factories; <see cref="OperationCanceledException"/>
/// triggered by the caller's <see cref="CancellationToken"/> is the only
/// path that is allowed to throw rather than become a
/// <see cref="Result{T}.Failure(DomainError[])"/>.
/// </summary>
/// <remarks>
/// Error codes are stable strings prefixed with <c>llm.</c> so HTTP
/// callers and log aggregators can match on them; the human messages may
/// change without breaking that contract.
/// </remarks>
public static class LlmErrors
{
    /// <summary>API key missing or rejected by the upstream (HTTP 401).</summary>
    public static DomainError AuthenticationFailed(string detail) =>
        new(Code: "llm.authenticationFailed", Message: detail);

    /// <summary>Upstream returned 429 Too Many Requests.</summary>
    public static DomainError RateLimited(string detail) =>
        new(Code: "llm.rateLimited", Message: detail);

    /// <summary>Upstream returned 5xx, refused the connection, timed out, or DNS failed.</summary>
    public static DomainError UpstreamUnavailable(string detail) =>
        new(Code: "llm.upstreamUnavailable", Message: detail);

    /// <summary>Upstream returned 4xx other than 401 / 429, or the request itself was malformed.</summary>
    public static DomainError InvalidRequest(string detail) =>
        new(Code: "llm.invalidRequest", Message: detail);
}
