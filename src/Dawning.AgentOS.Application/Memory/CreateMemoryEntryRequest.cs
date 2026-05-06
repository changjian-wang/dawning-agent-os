namespace Dawning.AgentOS.Application.Memory;

/// <summary>
/// Input for <c>POST /api/memory</c> per ADR-033 §决策 J1.
/// V0 only accepts <see cref="Domain.Memory.MemorySource.UserExplicit"/>
/// writes (§决策 B1), so the request DTO does not expose source /
/// is_explicit / confidence — the AppService forces those values.
/// </summary>
/// <remarks>
/// Per ADR-023 architectural rule the Application DTO surface stays
/// off the Dawning.AgentOS.Domain assembly so that Api can keep its
/// reference graph free of Domain. <see cref="Sensitivity"/> is
/// therefore a string parsed by the AppService into
/// <see cref="Domain.Memory.MemorySensitivity"/>; unknown values
/// surface as <c>memory.sensitivity.invalid</c> (HTTP 400).
/// </remarks>
/// <param name="Content">Memory content; required, non-whitespace, ≤ 4096 chars.</param>
/// <param name="Scope">Optional override of the default scope <c>"global"</c>; ≤ 128 chars.</param>
/// <param name="Sensitivity">Optional sensitivity tier (case-insensitive: <c>"Normal"</c> / <c>"Sensitive"</c> / <c>"HighSensitive"</c>); defaults to <c>Normal</c> when null.</param>
public sealed record CreateMemoryEntryRequest(string Content, string? Scope, string? Sensitivity);
