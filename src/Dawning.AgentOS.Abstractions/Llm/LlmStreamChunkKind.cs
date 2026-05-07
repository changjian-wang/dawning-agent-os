namespace Dawning.AgentOS.Abstractions.Llm;

/// <summary>
/// Discriminator for <see cref="LlmStreamChunk"/>. Per ADR-032 §决策 F1
/// streaming surfaces three logical events; per ADR-038 §决策 D2 a
/// fourth bypass event is added to carry memory-injection annotations
/// without polluting the LLM reply text:
/// <list type="bullet">
///   <item>
///     <description>
///       <see cref="Delta"/> — an incremental piece of generated text
///       (the SSE <c>chunk</c> event the API forwards to clients).
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="Done"/> — the upstream stream ended successfully;
///       this chunk carries the final <see cref="LlmStreamChunk.Model"/>,
///       <see cref="LlmStreamChunk.PromptTokens"/>,
///       <see cref="LlmStreamChunk.CompletionTokens"/> and
///       <see cref="LlmStreamChunk.Latency"/> snapshot.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="Error"/> — the upstream call failed mid-stream; the
///       chunk's <see cref="LlmStreamChunk.Error"/> carries the
///       <c>llm.*</c> code from <c>LlmErrors</c>. The provider raises
///       at most one <see cref="Error"/> chunk per stream and stops.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="MemoryAnnotation"/> — per ADR-038 §决策 D2 a
///       single bypass chunk emitted by <c>ChatAppService</c> (NOT by
///       <c>ILlmProvider</c>) before any <see cref="Delta"/>, carrying
///       the list of memory entries the orchestrator decided to inject
///       into the system prompt. The chunk's
///       <see cref="LlmStreamChunk.MemoryAnnotations"/> is populated;
///       all other discriminated fields are <c>null</c> /
///       <see cref="TimeSpan.Zero"/>. The API layer translates this
///       into an <c>event: memory-annotation</c> SSE frame; provider
///       implementations MUST NOT emit it.
///     </description>
///   </item>
/// </list>
/// </summary>
public enum LlmStreamChunkKind
{
    /// <summary>An incremental delta of generated text.</summary>
    Delta = 1,

    /// <summary>End-of-stream marker carrying the final usage / model snapshot.</summary>
    Done = 2,

    /// <summary>Mid-stream failure carrying an <c>llm.*</c> <c>DomainError</c>.</summary>
    Error = 3,

    /// <summary>
    /// Per ADR-038 §决策 D2 a side-channel chunk carrying the list of
    /// memory entries the orchestrator injected into the system prompt.
    /// Emitted by <c>ChatAppService</c>, never by <c>ILlmProvider</c>.
    /// </summary>
    MemoryAnnotation = 4,
}
