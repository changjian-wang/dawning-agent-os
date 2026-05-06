namespace Dawning.AgentOS.Application.Inbox;

/// <summary>
/// Read-side projection of a single <see cref="Dawning.AgentOS.Domain.Inbox.InboxItem"/>
/// returned by <see cref="Interfaces.IInboxAppService.CaptureAsync"/> and
/// <see cref="Interfaces.IInboxAppService.ListAsync"/>. Per ADR-026 §6
/// V0 only surfaces the persisted columns; future derived fields (tags,
/// summary, category) belong to a separate read-side projection, not
/// this DTO.
/// </summary>
/// <param name="Id">UUIDv7 identifier (canonical 36-char Guid string when serialized).</param>
/// <param name="Content">Captured content.</param>
/// <param name="Source">Optional capture-route marker.</param>
/// <param name="CapturedAtUtc">UTC instant the material was captured.</param>
/// <param name="CreatedAt">UTC creation instant; equals <paramref name="CapturedAtUtc"/> in V0.</param>
public sealed record InboxItemSnapshot(
    Guid Id,
    string Content,
    string? Source,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset CreatedAt
);
