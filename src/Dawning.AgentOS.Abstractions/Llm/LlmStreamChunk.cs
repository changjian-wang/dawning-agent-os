using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Abstractions.Llm;

/// <summary>
/// A single chunk in an LLM streaming response. Per ADR-032 §决策 F1
/// the streaming port surfaces a sequence of these; the
/// <see cref="Kind"/> discriminator tells the consumer which fields are
/// meaningful. Per ADR-038 §决策 D2 a fourth bypass kind
/// (<see cref="LlmStreamChunkKind.MemoryAnnotation"/>) is multiplexed
/// onto the same stream by the orchestrator (NOT the provider) to
/// surface memory citations without polluting the LLM reply text.
/// </summary>
/// <param name="Kind">Discriminator: <c>Delta</c> (text fragment), <c>Done</c> (end of stream + usage), <c>Error</c> (mid-stream failure), or <c>MemoryAnnotation</c> (side-channel citation list).</param>
/// <param name="Delta">For <see cref="LlmStreamChunkKind.Delta"/>: the incremental text piece. <c>null</c> for the other kinds.</param>
/// <param name="Model">For <see cref="LlmStreamChunkKind.Done"/>: the model identifier echoed by the provider. <c>null</c> for the other kinds.</param>
/// <param name="PromptTokens">For <see cref="LlmStreamChunkKind.Done"/>: prompt-token count when reported; otherwise <c>null</c>.</param>
/// <param name="CompletionTokens">For <see cref="LlmStreamChunkKind.Done"/>: completion-token count when reported; otherwise <c>null</c>.</param>
/// <param name="Latency">For <see cref="LlmStreamChunkKind.Done"/>: wall-clock time elapsed since the request was sent. <see cref="TimeSpan.Zero"/> for the other kinds.</param>
/// <param name="Error">For <see cref="LlmStreamChunkKind.Error"/>: the <c>llm.*</c> error from <c>LlmErrors</c>. <c>null</c> for the other kinds.</param>
/// <param name="MemoryAnnotations">
/// Per ADR-038 §决策 D2 — for <see cref="LlmStreamChunkKind.MemoryAnnotation"/>:
/// the non-empty list of memory entries the orchestrator injected.
/// <c>null</c> for the other kinds. The orchestrator MUST NOT emit a
/// <see cref="LlmStreamChunkKind.MemoryAnnotation"/> chunk with an empty
/// or null list — "no memories cited" is represented by simply omitting
/// the chunk so the renderer doesn't draw the muted hint at all.
/// </param>
public sealed record LlmStreamChunk(
    LlmStreamChunkKind Kind,
    string? Delta,
    string? Model,
    int? PromptTokens,
    int? CompletionTokens,
    TimeSpan Latency,
    DomainError? Error,
    IReadOnlyList<MemoryAnnotationItem>? MemoryAnnotations
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
            Error: null,
            MemoryAnnotations: null
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
            Error: null,
            MemoryAnnotations: null
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
            Error: error,
            MemoryAnnotations: null
        );
    }

    /// <summary>
    /// Per ADR-038 §决策 D2 factory for the side-channel memory
    /// annotation chunk. <paramref name="annotations"/> must be
    /// non-empty — an empty annotation chunk is meaningless and the
    /// orchestrator must skip it so the renderer renders no hint at all.
    /// </summary>
    /// <param name="annotations">
    /// Non-null, non-empty list of <see cref="MemoryAnnotationItem"/>
    /// describing the memory entries injected into the system prompt.
    /// </param>
    public static LlmStreamChunk ForMemoryAnnotation(
        IReadOnlyList<MemoryAnnotationItem> annotations
    )
    {
        ArgumentNullException.ThrowIfNull(annotations);
        if (annotations.Count == 0)
        {
            throw new ArgumentException(
                "MemoryAnnotation chunk must carry at least one entry; emit no chunk when the cited list is empty.",
                nameof(annotations)
            );
        }

        return new LlmStreamChunk(
            Kind: LlmStreamChunkKind.MemoryAnnotation,
            Delta: null,
            Model: null,
            PromptTokens: null,
            CompletionTokens: null,
            Latency: TimeSpan.Zero,
            Error: null,
            MemoryAnnotations: annotations
        );
    }
}
