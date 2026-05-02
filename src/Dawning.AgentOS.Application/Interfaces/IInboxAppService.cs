using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Application service facade for the inbox capture / list use cases. Per
/// ADR-026 §6 V0 exposes exactly two operations; update and delete are
/// deliberately out of scope until a UX entry exists for them.
/// </summary>
public interface IInboxAppService
{
    /// <summary>
    /// Captures a new piece of material into the inbox. The AppService
    /// stamps <c>capturedAtUtc</c> from the clock; callers cannot pass
    /// arbitrary timestamps.
    /// </summary>
    /// <param name="request">Capture input; <see cref="CaptureInboxItemRequest.Content"/> must be non-empty.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Success"/> with the persisted snapshot, or
    /// <see cref="Result{T}.Failure"/> with field-level errors when
    /// validation fails (mapped to HTTP 400 by the API layer).
    /// </returns>
    Task<Result<InboxItemSnapshot>> CaptureAsync(
        CaptureInboxItemRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns a page of recently captured items, ordered most recent
    /// first per ADR-026 §4.
    /// </summary>
    /// <param name="query">Pagination input; bounds-checked by the AppService.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Success"/> with the page, or
    /// <see cref="Result{T}.Failure"/> with field-level errors when
    /// limit / offset are out of range (mapped to HTTP 400).
    /// </returns>
    Task<Result<InboxListPage>> ListAsync(
        InboxListQuery query,
        CancellationToken cancellationToken
    );
}
