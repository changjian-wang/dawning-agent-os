using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Inbox;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Memory;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Domain.Inbox;
using Dawning.AgentOS.Domain.Memory;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IInboxToMemoryAppService"/>. Per
/// ADR-034 the service:
/// <list type="number">
///   <item>
///     <description>
///       loads the inbox aggregate via
///       <see cref="IInboxRepository.GetByIdAsync"/>; absence becomes
///       <c>inbox.notFound</c> (HTTP 404 in the API layer);
///     </description>
///   </item>
///   <item>
///     <description>
///       calls <see cref="MemoryLedgerEntry.Create"/> directly with the
///       fixed promotion shape (<see cref="MemorySource.InboxAction"/>,
///       <c>isExplicit=true</c>, <c>confidence=1.0</c>,
///       <see cref="MemorySensitivity.Normal"/>, scope =
///       <see cref="PromotionScope"/>) — ADR-033 §决策 B1's
///       <c>UserExplicit</c> rail in the memory AppService is
///       intentionally bypassed because this is a separate write path
///       per ADR-034 §决策 A1 / D1;
///     </description>
///   </item>
///   <item>
///     <description>
///       persists via <see cref="IMemoryLedgerRepository.AddAsync"/> and
///       projects the aggregate to <see cref="MemoryEntryDto"/> for the
///       caller — V0 does not dedup, does not query existing rows, and
///       does not record a back-reference column (ADR-034 §决策 F1).
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-034 §决策 D1 the class is registered scoped via the existing
/// <c>AddApplication()</c> reflection-based scan in
/// <c>ApplicationServiceCollectionExtensions</c>; no manual DI line is
/// required because the class follows the <c>IXxxAppService</c> /
/// <c>XxxAppService</c> naming convention.
/// </para>
/// <para>
/// <see cref="OperationCanceledException"/> from the repository calls is
/// propagated, mirroring the existing inbox / memory AppServices.
/// </para>
/// </remarks>
public sealed class InboxToMemoryAppService(
    IClock clock,
    IInboxRepository inboxRepository,
    IMemoryLedgerRepository memoryRepository
) : IInboxToMemoryAppService
{
    /// <summary>
    /// Per ADR-034 §决策 C1 every promoted entry receives this fixed
    /// scope value, regardless of <c>InboxItem.Source</c>. The decision
    /// is intentional: <c>InboxItem.Source</c> is a free-text field
    /// (ADR-026), and routing it into <c>MemoryLedgerEntry.Scope</c>
    /// would pollute that dimension before the controlled vocabulary
    /// settles.
    /// </summary>
    public const string PromotionScope = "inbox";

    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IInboxRepository _inboxRepository =
        inboxRepository ?? throw new ArgumentNullException(nameof(inboxRepository));
    private readonly IMemoryLedgerRepository _memoryRepository =
        memoryRepository ?? throw new ArgumentNullException(nameof(memoryRepository));

    /// <inheritdoc />
    public async Task<Result<MemoryEntryDto>> PromoteAsync(
        Guid inboxItemId,
        CancellationToken cancellationToken
    )
    {
        var inbox = await _inboxRepository
            .GetByIdAsync(inboxItemId, cancellationToken)
            .ConfigureAwait(false);

        if (inbox is null)
        {
            return Result<MemoryEntryDto>.Failure(InboxErrors.ItemNotFound(inboxItemId));
        }

        var entry = MemoryLedgerEntry.Create(
            content: inbox.Content,
            scope: PromotionScope,
            source: MemorySource.InboxAction,
            isExplicit: true,
            confidence: 1.0,
            sensitivity: MemorySensitivity.Normal,
            createdAtUtc: _clock.UtcNow
        );

        await _memoryRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);

        if (entry.DomainEvents.Count > 0)
        {
            entry.ClearDomainEvents();
        }

        return Result<MemoryEntryDto>.Success(ToDto(entry));
    }

    private static MemoryEntryDto ToDto(MemoryLedgerEntry entry) =>
        new(
            Id: entry.Id,
            Content: entry.Content,
            Scope: entry.Scope,
            Source: entry.Source.ToString(),
            IsExplicit: entry.IsExplicit,
            Confidence: entry.Confidence,
            Sensitivity: entry.Sensitivity.ToString(),
            Status: entry.Status.ToString(),
            CreatedAt: entry.CreatedAt,
            UpdatedAt: entry.UpdatedAtUtc,
            DeletedAt: entry.DeletedAtUtc
        );
}
