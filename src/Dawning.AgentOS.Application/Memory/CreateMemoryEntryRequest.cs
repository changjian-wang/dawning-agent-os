namespace Dawning.AgentOS.Application.Memory;

/// <summary>
/// Input for <c>POST /api/memory</c> per ADR-033 §决策 J1.
/// V0 only accepts <see cref="Domain.Memory.MemorySource.UserExplicit"/>
/// writes (§决策 B1), so the request DTO does not expose source /
/// is_explicit / confidence — the AppService forces those values.
/// </summary>
/// <param name="Content">Memory content; required, non-whitespace, ≤ 4096 chars.</param>
/// <param name="Scope">Optional override of the default scope <c>"global"</c>; ≤ 128 chars.</param>
/// <param name="Sensitivity">Optional sensitivity tier; defaults to <c>Normal</c>.</param>
public sealed record CreateMemoryEntryRequest(
    string Content,
    string? Scope,
    Domain.Memory.MemorySensitivity? Sensitivity
);
