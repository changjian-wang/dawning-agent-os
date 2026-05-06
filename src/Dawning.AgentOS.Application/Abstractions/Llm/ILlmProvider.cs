using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Abstractions.Llm;

/// <summary>
/// Outbound port for chat-completion calls to an LLM provider. Per
/// ADR-028 §决策 A1 / B1 V0 surfaces a single non-streaming method;
/// streaming, tool calling, and structured output are deferred to
/// future ADRs and explicit consumers.
/// </summary>
/// <remarks>
/// <para>
/// The implementation lives in
/// <c>Dawning.AgentOS.Infrastructure.Llm</c>; the architecture test
/// <c>Application_DoesNotReferenceFrameworkAdapterPackages</c> together
/// with the namespace placement guards keep that boundary intact.
/// </para>
/// <para>
/// Errors are returned through <see cref="Result{T}"/> rather than
/// thrown, mirroring the error model fixed in ADR-026 for the inbox
/// AppService. <see cref="OperationCanceledException"/> raised by the
/// caller's <see cref="CancellationToken"/> is the only failure that is
/// allowed to propagate.
/// </para>
/// </remarks>
public interface ILlmProvider
{
    /// <summary>
    /// The configured display name of this provider (<c>"OpenAI"</c> or
    /// <c>"DeepSeek"</c>). Endpoint-level callers (e.g. the
    /// <c>/api/llm/ping</c> smoke endpoint) include the value verbatim
    /// in their response payloads so the UI can surface which provider
    /// served the call without re-reading configuration.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends a single non-streaming chat-completion request and returns
    /// the assistant's response.
    /// </summary>
    /// <param name="request">The request DTO; <see cref="LlmRequest.Messages"/> must contain at least one entry.</param>
    /// <param name="cancellationToken">Cooperative cancellation token; flowed end-to-end into the upstream HTTP call.</param>
    /// <returns>
    /// On success, <see cref="Result{T}.Success(T)"/> with an
    /// <see cref="LlmCompletion"/>. On business failure, one of the
    /// errors defined in <see cref="LlmErrors"/>.
    /// </returns>
    Task<Result<LlmCompletion>> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken
    );
}
