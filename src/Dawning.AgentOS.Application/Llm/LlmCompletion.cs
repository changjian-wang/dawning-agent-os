namespace Dawning.AgentOS.Application.Llm;

/// <summary>
/// The successful outcome of an <see cref="ILlmProvider.CompleteAsync"/>
/// call. Per ADR-028 §决策 B1 the V0 surface carries the assistant's
/// content text plus enough provenance fields (model identifier from the
/// provider's response, usage counters when reported, wall-clock latency)
/// for the caller to debug or audit; finish reason, log probabilities,
/// system fingerprint, and tool-call payloads are intentionally omitted.
/// </summary>
/// <param name="Content">The assistant's response text.</param>
/// <param name="Model">Model identifier echoed by the provider (may differ from the requested model when the provider routes).</param>
/// <param name="PromptTokens">Tokens consumed by the prompt; null when the provider does not report usage.</param>
/// <param name="CompletionTokens">Tokens produced in the completion; null when the provider does not report usage.</param>
/// <param name="Latency">Wall-clock latency from request send to response received, measured by the provider adapter.</param>
public sealed record LlmCompletion(
    string Content,
    string Model,
    int? PromptTokens,
    int? CompletionTokens,
    TimeSpan Latency
);
