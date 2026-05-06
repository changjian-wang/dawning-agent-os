namespace Dawning.AgentOS.Application.Llm;

/// <summary>
/// A single chat message in an <see cref="LlmRequest"/>. Per ADR-028
/// §决策 B1 the message is a value-typed pair of role and verbatim
/// content; no token counts, no metadata, no tool-call payload — these
/// are deferred until a future ADR introduces tool calling / streaming.
/// </summary>
/// <param name="Role">The speaker of this turn.</param>
/// <param name="Content">The verbatim text of the turn; never null, may be empty for assistant placeholders.</param>
public sealed record LlmMessage(LlmRole Role, string Content);
