using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Domain.Inbox;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IInboxAppService"/>. Per
/// ADR-026 §6 the service:
/// <list type="number">
///   <item>
///     <description>
///       validates input DTOs and surfaces business errors as
///       <see cref="Result.Failure(DomainError[])"/> with field-level
///       <c>field</c> entries (mapped by
///       <c>ResultHttpExtensions.ToHttpResult</c> to HTTP 400);
///     </description>
///   </item>
///   <item>
///     <description>
///       stamps <see cref="InboxItem.CapturedAtUtc"/> from
///       <see cref="IClock.UtcNow"/> rather than accepting client-supplied
///       timestamps;
///     </description>
///   </item>
///   <item>
///     <description>
///       persists via <see cref="IInboxRepository"/> and clears domain
///       events without dispatch — V0 has no dispatcher (ADR-022 §10
///       still open), so events are intentionally not delivered to any
///       handler. The Application layer cannot depend on the logging
///       package per ADR-021 / LayeringTests, so the "no dispatcher"
///       audit trail is left to a future logging-side adapter wired
///       through a port.
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class InboxAppService(IClock clock, IInboxRepository repository) : IInboxAppService
{
    /// <summary>Per ADR-026 §C2 lower bound on the list page size.</summary>
    public const int MinListLimit = 1;

    /// <summary>Per ADR-026 §C2 upper bound on the list page size.</summary>
    public const int MaxListLimit = 200;

    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IInboxRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public async Task<Result<InboxItemSnapshot>> CaptureAsync(
        CaptureInboxItemRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            validationErrors.Add(
                new DomainError(
                    Code: "inbox.content.required",
                    Message: "Inbox content is required and must not be whitespace.",
                    Field: "content"
                )
            );
        }
        else if (request.Content.Length > InboxItem.MaxContentLength)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "inbox.content.tooLong",
                    Message: $"Inbox content must be {InboxItem.MaxContentLength} characters or fewer.",
                    Field: "content"
                )
            );
        }

        if (request.Source is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Source))
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "inbox.source.invalid",
                        Message: "Inbox source, when provided, must be non-whitespace.",
                        Field: "source"
                    )
                );
            }
            else if (request.Source.Length > InboxItem.MaxSourceLength)
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "inbox.source.tooLong",
                        Message: $"Inbox source must be {InboxItem.MaxSourceLength} characters or fewer.",
                        Field: "source"
                    )
                );
            }
        }

        if (validationErrors.Count > 0)
        {
            return Result<InboxItemSnapshot>.Failure(validationErrors.ToArray());
        }

        var capturedAt = _clock.UtcNow;
        var item = InboxItem.Capture(request.Content, request.Source, capturedAt);

        await _repository.AddAsync(item, cancellationToken).ConfigureAwait(false);

        // ADR-026 §3 / §6: V0 has no dispatcher; we still drain the
        // event queue so the aggregate doesn't carry stale events
        // across method boundaries. The Application layer cannot take a
        // logger dependency (ADR-021 / LayeringTests), so the "raised
        // but not dispatched" audit trail is intentionally silent until
        // the dispatcher closure ADR (ADR-022 §10) lands.
        if (item.DomainEvents.Count > 0)
        {
            item.ClearDomainEvents();
        }

        return Result<InboxItemSnapshot>.Success(ToSnapshot(item));
    }

    /// <inheritdoc />
    public async Task<Result<InboxListPage>> ListAsync(
        InboxListQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var validationErrors = new List<DomainError>();
        if (query.Limit < MinListLimit || query.Limit > MaxListLimit)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "inbox.limit.outOfRange",
                    Message: $"limit must be between {MinListLimit} and {MaxListLimit}.",
                    Field: "limit"
                )
            );
        }

        if (query.Offset < 0)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "inbox.offset.outOfRange",
                    Message: "offset must be greater than or equal to 0.",
                    Field: "offset"
                )
            );
        }

        if (validationErrors.Count > 0)
        {
            return Result<InboxListPage>.Failure(validationErrors.ToArray());
        }

        var items = await _repository
            .ListAsync(query.Limit, query.Offset, cancellationToken)
            .ConfigureAwait(false);
        var total = await _repository.CountAsync(cancellationToken).ConfigureAwait(false);

        var snapshots = new List<InboxItemSnapshot>(items.Count);
        foreach (var item in items)
        {
            snapshots.Add(ToSnapshot(item));
        }

        var page = new InboxListPage(
            Items: snapshots,
            Total: total,
            Limit: query.Limit,
            Offset: query.Offset
        );

        return Result<InboxListPage>.Success(page);
    }

    private static InboxItemSnapshot ToSnapshot(InboxItem item) =>
        new(item.Id, item.Content, item.Source, item.CapturedAtUtc, item.CreatedAt);
}
