namespace Dawning.AgentOS.Abstractions.Llm;

/// <summary>
/// Per ADR-038 §决策 D2 a single memory entry the orchestrator decided
/// to inject into the system prompt of a chat call. Carried inside
/// <see cref="LlmStreamChunk.MemoryAnnotations"/> on the side-channel
/// <see cref="LlmStreamChunkKind.MemoryAnnotation"/> chunk.
/// </summary>
/// <param name="Id">
/// The full <see cref="Guid"/> of the underlying
/// <c>MemoryLedgerEntry</c>. The renderer's expanded view shows the
/// first 8 characters as a debug-friendly short id; full id is kept in
/// the payload so program-side consumers can navigate to the Memory
/// view.
/// </param>
/// <param name="ContentPreview">
/// Per ADR-038 §决策 D2 + the §plan-first §Q4 answer (80 characters),
/// the first 80 characters of the entry content with a trailing
/// <c>"…"</c> when truncated. The full content is intentionally NOT
/// shipped: D2 is a "did we cite something" hint, not a content
/// browser — that role belongs to the Memory view.
/// </param>
public sealed record MemoryAnnotationItem(Guid Id, string ContentPreview);
