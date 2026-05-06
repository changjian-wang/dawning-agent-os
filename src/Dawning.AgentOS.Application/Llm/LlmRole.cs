namespace Dawning.AgentOS.Application.Llm;

/// <summary>
/// The role of a single message in a chat completion request. Per
/// ADR-028 §决策 B1 V0 supports only the three OpenAI-compatible
/// canonical roles; <c>tool</c> / <c>function</c> roles are deferred
/// until tool calling is decided in a future ADR.
/// </summary>
public enum LlmRole
{
    /// <summary>System message: high-priority instructions for the assistant.</summary>
    System = 0,

    /// <summary>User message: the human-facing turn.</summary>
    User = 1,

    /// <summary>Assistant message: prior model output replayed in the conversation.</summary>
    Assistant = 2,
}
