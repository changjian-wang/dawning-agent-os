namespace Dawning.AgentOS.Application.Llm;

/// <summary>
/// Input for a single non-streaming chat completion call. Per ADR-028
/// §决策 B1 V0 exposes exactly four fields; tools, response format,
/// streaming, seed, and stop sequences are deferred to a future ADR.
/// </summary>
/// <remarks>
/// <para>
/// All sampling parameters are nullable: when <c>null</c> the provider
/// falls back to the model's natural default (rather than .NET's default
/// <c>0</c>). This matches the convention used by Microsoft.Extensions.AI
/// and OpenAI Agents SDK; different models have different sane defaults
/// (e.g. reasoning models reject custom temperature) so the safe default
/// is "do not transmit the field".
/// </para>
/// <para>
/// <see cref="Model"/> overrides the active provider's configured
/// default model. When <c>null</c> the provider uses
/// <c>Llm:Providers:&lt;ActiveProvider&gt;:Model</c> from configuration.
/// </para>
/// </remarks>
/// <param name="Messages">Ordered conversation turns; must contain at least one entry.</param>
/// <param name="Model">Optional per-call model override; null = use provider's configured default.</param>
/// <param name="Temperature">Optional sampling temperature; null = model default.</param>
/// <param name="MaxTokens">Optional cap on completion length in tokens; null = model default.</param>
public sealed record LlmRequest(
    IReadOnlyList<LlmMessage> Messages,
    string? Model,
    double? Temperature,
    int? MaxTokens
);
