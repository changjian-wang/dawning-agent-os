namespace Dawning.AgentOS.Domain.Chat;

/// <summary>
/// The role a <see cref="ChatMessage"/> plays in a chat conversation.
/// Per ADR-032 §决策 D1 / E1 the V0 schema only persists user-authored
/// and assistant-generated turns; the built-in system prompt is
/// prepended by the AppService at request time and never written to
/// the <c>chat_messages</c> table.
/// </summary>
public enum ChatRole
{
    /// <summary>A turn authored by the human user.</summary>
    User = 1,

    /// <summary>A turn produced by the LLM.</summary>
    Assistant = 2,
}
