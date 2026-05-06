using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Domain.Inbox;

/// <summary>
/// Domain event raised when a new <see cref="InboxItem"/> is captured
/// via <see cref="InboxItem.Capture(string, string?, DateTimeOffset)"/>.
/// </summary>
/// <remarks>
/// Per ADR-026 §3 V0 has no handler subscribed; the AppService logs the
/// raise and clears events without dispatch. The actual dispatcher
/// implementation is tracked under ADR-022 §10 and is not closed by this
/// ADR. The event is still defined so the eventual handler has a stable
/// type to subscribe to without changing the aggregate.
/// </remarks>
/// <param name="InboxItemId">The captured item's UUIDv7 identifier.</param>
/// <param name="CapturedAtUtc">UTC instant of the capture business action.</param>
/// <param name="OccurredOn">UTC instant the event was raised; equals <paramref name="CapturedAtUtc"/> in V0.</param>
public sealed record InboxItemCaptured(
    Guid InboxItemId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset OccurredOn
) : IDomainEvent;
