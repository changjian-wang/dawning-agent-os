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

    /// <summary>
    /// Sends a streaming chat-completion request and yields each
    /// upstream event as an <see cref="LlmStreamChunk"/>. Per ADR-032
    /// §决策 F1 the contract is:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       Zero or more <see cref="LlmStreamChunkKind.Delta"/> chunks
    ///       arrive in arrival order.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       The stream is terminated by exactly one
    ///       <see cref="LlmStreamChunkKind.Done"/> chunk on success, or
    ///       exactly one <see cref="LlmStreamChunkKind.Error"/> chunk on
    ///       failure. Implementations must not yield further chunks
    ///       after the terminal one.
    ///     </description>
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="request">Same shape as for <see cref="CompleteAsync"/>; <see cref="LlmRequest.Messages"/> must be non-empty.</param>
    /// <param name="cancellationToken">Cooperative cancellation token; cancellation surfaces as <see cref="OperationCanceledException"/> propagated to the caller (the only allowed-to-throw exception per ADR-028 §决策 G1).</param>
    /// <returns>An asynchronous sequence of stream events.</returns>
    IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
        LlmRequest request,
        CancellationToken cancellationToken
    );
}
