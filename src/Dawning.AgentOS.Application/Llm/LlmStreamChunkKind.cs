namespace Dawning.AgentOS.Application.Llm;

/// <summary>
/// Discriminator for <see cref="LlmStreamChunk"/>. Per ADR-032 §决策 F1
/// streaming surfaces three logical events:
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
}
