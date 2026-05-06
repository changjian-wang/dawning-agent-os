using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Llm;

/// <summary>
/// A single chunk in an LLM streaming response. Per ADR-032 §决策 F1
/// the streaming port surfaces a sequence of these; the
/// <see cref="Kind"/> discriminator tells the consumer which fields are
/// meaningful.
/// </summary>
/// <param name="Kind">Discriminator: <c>Delta</c> (text fragment), <c>Done</c> (end of stream + usage), or <c>Error</c> (mid-stream failure).</param>
/// <param name="Delta">For <see cref="LlmStreamChunkKind.Delta"/>: the incremental text piece. <c>null</c> for the other kinds.</param>
/// <param name="Model">For <see cref="LlmStreamChunkKind.Done"/>: the model identifier echoed by the provider. <c>null</c> for the other kinds.</param>
/// <param name="PromptTokens">For <see cref="LlmStreamChunkKind.Done"/>: prompt-token count when reported; otherwise <c>null</c>.</param>
/// <param name="CompletionTokens">For <see cref="LlmStreamChunkKind.Done"/>: completion-token count when reported; otherwise <c>null</c>.</param>
/// <param name="Latency">For <see cref="LlmStreamChunkKind.Done"/>: wall-clock time elapsed since the request was sent. <see cref="TimeSpan.Zero"/> for the other kinds.</param>
/// <param name="Error">For <see cref="LlmStreamChunkKind.Error"/>: the <c>llm.*</c> error from <c>LlmErrors</c>. <c>null</c> for the other kinds.</param>
public sealed record LlmStreamChunk(
    LlmStreamChunkKind Kind,
    string? Delta,
    string? Model,
    int? PromptTokens,
    int? CompletionTokens,
    TimeSpan Latency,
    DomainError? Error
)
{
    /// <summary>Factory for a delta chunk; the only one that carries text.</summary>
    public static LlmStreamChunk ForDelta(string delta) =>
        new(
            Kind: LlmStreamChunkKind.Delta,
            Delta: delta,
            Model: null,
            PromptTokens: null,
            CompletionTokens: null,
            Latency: TimeSpan.Zero,
            Error: null
        );

    /// <summary>Factory for the end-of-stream chunk; carries the final usage snapshot.</summary>
    public static LlmStreamChunk ForDone(
        string model,
        int? promptTokens,
        int? completionTokens,
        TimeSpan latency
    ) =>
        new(
            Kind: LlmStreamChunkKind.Done,
            Delta: null,
            Model: model,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            Latency: latency,
            Error: null
        );

    /// <summary>Factory for a mid-stream failure chunk.</summary>
    public static LlmStreamChunk ForError(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LlmStreamChunk(
            Kind: LlmStreamChunkKind.Error,
            Delta: null,
            Model: null,
            PromptTokens: null,
            CompletionTokens: null,
            Latency: TimeSpan.Zero,
            Error: error
        );
    }
}
