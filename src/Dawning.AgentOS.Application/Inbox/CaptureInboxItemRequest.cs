namespace Dawning.AgentOS.Application.Inbox;

/// <summary>
/// Request DTO for capturing a new inbox item. Per ADR-026 §6 the
/// caller does <em>not</em> supply the capture timestamp; the
/// AppService stamps <c>capturedAtUtc</c> from <see cref="Dawning.AgentOS.Application.Abstractions.IClock"/>
/// to keep the host as the single source of time truth.
/// </summary>
/// <param name="Content">The user-supplied material; required, ≤ 4096 chars.</param>
/// <param name="Source">
/// Optional capture-route marker (<c>"chat"</c>, <c>"clipboard"</c>, …);
/// when provided must be non-whitespace and ≤ 64 chars.
/// </param>
public sealed record CaptureInboxItemRequest(string Content, string? Source);
