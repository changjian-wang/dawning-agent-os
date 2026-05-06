using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Memory;
using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Domain.Memory;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IMemoryLedgerAppService"/>. Per
/// ADR-033 §决策 J1 the service:
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
///       stamps <c>createdAtUtc</c> / <c>updatedAtUtc</c> from
///       <see cref="IClock.UtcNow"/> rather than accepting client-supplied
///       timestamps;
///     </description>
///   </item>
///   <item>
///     <description>
///       forces <c>source = UserExplicit</c>, <c>isExplicit = true</c>,
///       <c>confidence = 1.0</c> on every create; future ADRs will
///       relax these rails when inference paths land;
///     </description>
///   </item>
///   <item>
///     <description>
///       catches <see cref="MemoryLedgerInvalidStatusTransitionException"/>
///       from the aggregate and re-emits it as
///       <see cref="MemoryErrors.InvalidStatusTransitionCode"/>
///       (HTTP 422 in the API layer).
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class MemoryLedgerAppService(IClock clock, IMemoryLedgerRepository repository)
    : IMemoryLedgerAppService
{
    /// <summary>Per ADR-033 §决策 J1 lower bound on the list page size.</summary>
    public const int MinListLimit = 1;

    /// <summary>Per ADR-033 §决策 J1 upper bound on the list page size.</summary>
    public const int MaxListLimit = 200;

    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IMemoryLedgerRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public async Task<Result<MemoryEntryDto>> CreateExplicitAsync(
        CreateMemoryEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = new List<DomainError>();

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            validationErrors.Add(
                new DomainError(
                    Code: "memory.content.required",
                    Message: "Memory content is required and must not be whitespace.",
                    Field: "content"
                )
            );
        }
        else if (request.Content.Length > MemoryLedgerEntry.MaxContentLength)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "memory.content.tooLong",
                    Message: $"Memory content must be {MemoryLedgerEntry.MaxContentLength} characters or fewer.",
                    Field: "content"
                )
            );
        }

        if (request.Scope is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Scope))
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "memory.scope.invalid",
                        Message: "Memory scope, when provided, must be non-whitespace.",
                        Field: "scope"
                    )
                );
            }
            else if (request.Scope.Length > MemoryLedgerEntry.MaxScopeLength)
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "memory.scope.tooLong",
                        Message: $"Memory scope must be {MemoryLedgerEntry.MaxScopeLength} characters or fewer.",
                        Field: "scope"
                    )
                );
            }
        }

        if (request.Sensitivity is not null)
        {
            if (!TryParseSensitivity(request.Sensitivity, out _))
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "memory.sensitivity.invalid",
                        Message: $"Memory sensitivity '{request.Sensitivity}' is not a defined MemorySensitivity value.",
                        Field: "sensitivity"
                    )
                );
            }
        }

        if (validationErrors.Count > 0)
        {
            return Result<MemoryEntryDto>.Failure(validationErrors.ToArray());
        }

        var createdAt = _clock.UtcNow;
        var sensitivity = request.Sensitivity is not null
            ? ParseSensitivityOrThrow(request.Sensitivity)
            : MemorySensitivity.Normal;

        var entry = MemoryLedgerEntry.Create(
            content: request.Content,
            scope: request.Scope,
            source: MemorySource.UserExplicit,
            isExplicit: true,
            confidence: 1.0,
            sensitivity: sensitivity,
            createdAtUtc: createdAt
        );

        await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);

        if (entry.DomainEvents.Count > 0)
        {
            entry.ClearDomainEvents();
        }

        return Result<MemoryEntryDto>.Success(ToDto(entry));
    }

    /// <inheritdoc />
    public async Task<Result<MemoryEntryListPage>> ListAsync(
        MemoryListQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var validationErrors = new List<DomainError>();

        if (query.Limit < MinListLimit || query.Limit > MaxListLimit)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "memory.limit.outOfRange",
                    Message: $"limit must be between {MinListLimit} and {MaxListLimit}.",
                    Field: "limit"
                )
            );
        }

        if (query.Offset < 0)
        {
            validationErrors.Add(
                new DomainError(
                    Code: "memory.offset.outOfRange",
                    Message: "offset must be greater than or equal to 0.",
                    Field: "offset"
                )
            );
        }

        if (query.Status is { } statusText && !TryParseStatus(statusText, out _))
        {
            validationErrors.Add(
                new DomainError(
                    Code: "memory.status.invalid",
                    Message: $"Memory status '{statusText}' is not a defined MemoryStatus value.",
                    Field: "status"
                )
            );
        }

        if (validationErrors.Count > 0)
        {
            return Result<MemoryEntryListPage>.Failure(validationErrors.ToArray());
        }

        MemoryStatus? statusFilter = query.Status is null ? null : ParseStatusOrThrow(query.Status);

        var items = await _repository
            .ListAsync(
                statusFilter,
                query.IncludeSoftDeleted,
                query.Limit,
                query.Offset,
                cancellationToken
            )
            .ConfigureAwait(false);

        var total = await _repository
            .CountAsync(statusFilter, query.IncludeSoftDeleted, cancellationToken)
            .ConfigureAwait(false);

        var dtos = new List<MemoryEntryDto>(items.Count);
        foreach (var entry in items)
        {
            dtos.Add(ToDto(entry));
        }

        return Result<MemoryEntryListPage>.Success(
            new MemoryEntryListPage(dtos, total, query.Limit, query.Offset)
        );
    }

    /// <inheritdoc />
    public async Task<Result<MemoryEntryDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var entry = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return Result<MemoryEntryDto>.Failure(MemoryErrors.NotFound(id));
        }

        return Result<MemoryEntryDto>.Success(ToDto(entry));
    }

    /// <inheritdoc />
    public async Task<Result<MemoryEntryDto>> UpdateAsync(
        Guid id,
        UpdateMemoryEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (
            request.Content is null
            && request.Scope is null
            && request.Sensitivity is null
            && request.Status is null
        )
        {
            return Result<MemoryEntryDto>.Failure(MemoryErrors.UpdateEmpty());
        }

        var validationErrors = new List<DomainError>();

        if (request.Content is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "memory.content.required",
                        Message: "Memory content is required and must not be whitespace.",
                        Field: "content"
                    )
                );
            }
            else if (request.Content.Length > MemoryLedgerEntry.MaxContentLength)
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "memory.content.tooLong",
                        Message: $"Memory content must be {MemoryLedgerEntry.MaxContentLength} characters or fewer.",
                        Field: "content"
                    )
                );
            }
        }

        if (request.Scope is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Scope))
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "memory.scope.invalid",
                        Message: "Memory scope, when provided, must be non-whitespace.",
                        Field: "scope"
                    )
                );
            }
            else if (request.Scope.Length > MemoryLedgerEntry.MaxScopeLength)
            {
                validationErrors.Add(
                    new DomainError(
                        Code: "memory.scope.tooLong",
                        Message: $"Memory scope must be {MemoryLedgerEntry.MaxScopeLength} characters or fewer.",
                        Field: "scope"
                    )
                );
            }
        }

        if (request.Sensitivity is not null && !TryParseSensitivity(request.Sensitivity, out _))
        {
            validationErrors.Add(
                new DomainError(
                    Code: "memory.sensitivity.invalid",
                    Message: $"Memory sensitivity '{request.Sensitivity}' is not a defined MemorySensitivity value.",
                    Field: "sensitivity"
                )
            );
        }

        if (request.Status is not null && !TryParseStatus(request.Status, out _))
        {
            validationErrors.Add(
                new DomainError(
                    Code: "memory.status.invalid",
                    Message: $"Memory status '{request.Status}' is not a defined MemoryStatus value.",
                    Field: "status"
                )
            );
        }

        if (validationErrors.Count > 0)
        {
            return Result<MemoryEntryDto>.Failure(validationErrors.ToArray());
        }

        var entry = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return Result<MemoryEntryDto>.Failure(MemoryErrors.NotFound(id));
        }

        var updatedAt = _clock.UtcNow;

        try
        {
            if (request.Content is not null)
            {
                entry.UpdateContent(request.Content, updatedAt);
            }

            if (request.Scope is not null)
            {
                entry.UpdateScope(request.Scope, updatedAt);
            }

            if (request.Sensitivity is not null)
            {
                entry.UpdateSensitivity(ParseSensitivityOrThrow(request.Sensitivity), updatedAt);
            }

            if (request.Status is not null)
            {
                ApplyStatusTransition(entry, ParseStatusOrThrow(request.Status), updatedAt);
            }
        }
        catch (MemoryLedgerInvalidStatusTransitionException ex)
        {
            return Result<MemoryEntryDto>.Failure(
                MemoryErrors.InvalidStatusTransition(ex.CurrentStatus, ex.AttemptedAction)
            );
        }

        var rowUpdated = await _repository
            .UpdateAsync(entry, cancellationToken)
            .ConfigureAwait(false);
        if (!rowUpdated)
        {
            // Race: row vanished between GetById and Update; surface
            // as not-found rather than silently succeeding.
            return Result<MemoryEntryDto>.Failure(MemoryErrors.NotFound(id));
        }

        if (entry.DomainEvents.Count > 0)
        {
            entry.ClearDomainEvents();
        }

        return Result<MemoryEntryDto>.Success(ToDto(entry));
    }

    /// <inheritdoc />
    public async Task<Result<MemoryEntryDto>> SoftDeleteAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var entry = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return Result<MemoryEntryDto>.Failure(MemoryErrors.NotFound(id));
        }

        var updatedAt = _clock.UtcNow;

        try
        {
            entry.SoftDelete(updatedAt);
        }
        catch (MemoryLedgerInvalidStatusTransitionException ex)
        {
            return Result<MemoryEntryDto>.Failure(
                MemoryErrors.InvalidStatusTransition(ex.CurrentStatus, ex.AttemptedAction)
            );
        }

        var rowUpdated = await _repository
            .UpdateAsync(entry, cancellationToken)
            .ConfigureAwait(false);
        if (!rowUpdated)
        {
            return Result<MemoryEntryDto>.Failure(MemoryErrors.NotFound(id));
        }

        if (entry.DomainEvents.Count > 0)
        {
            entry.ClearDomainEvents();
        }

        return Result<MemoryEntryDto>.Success(ToDto(entry));
    }

    /// <summary>
    /// Translates a desired terminal status into the corresponding
    /// state-machine action on the aggregate. Per ADR-033 §决策 G1
    /// only the four explicit destinations are reachable via PATCH.
    /// </summary>
    private static void ApplyStatusTransition(
        MemoryLedgerEntry entry,
        MemoryStatus target,
        DateTimeOffset updatedAt
    )
    {
        if (entry.Status == target)
        {
            // No-op transition: aggregate state already matches.
            // We deliberately do NOT throw here so a PATCH that
            // sets status to its current value is a quiet success;
            // the renderer often issues such PATCHes when toggling
            // multiple fields at once.
            return;
        }

        switch (target)
        {
            case MemoryStatus.Active:
                entry.Restore(updatedAt);
                break;
            case MemoryStatus.Corrected:
                entry.MarkCorrected(updatedAt);
                break;
            case MemoryStatus.Archived:
                entry.Archive(updatedAt);
                break;
            case MemoryStatus.SoftDeleted:
                entry.SoftDelete(updatedAt);
                break;
            default:
                throw new MemoryLedgerInvalidStatusTransitionException(
                    entry.Status,
                    $"Set:{target}"
                );
        }
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

    /// <summary>
    /// Parses a sensitivity string (case-insensitive) and verifies it
    /// is a defined <see cref="MemorySensitivity"/> name. Numeric
    /// representations are rejected: only the named members are
    /// accepted on the wire so the API surface is stable across
    /// renumbering.
    /// </summary>
    private static bool TryParseSensitivity(string value, out MemorySensitivity result)
    {
        if (
            !Enum.TryParse(value, ignoreCase: true, out result)
            || !Enum.IsDefined(result)
            || IsNumericLiteral(value)
        )
        {
            result = default;
            return false;
        }

        return true;
    }

    private static bool TryParseStatus(string value, out MemoryStatus result)
    {
        if (
            !Enum.TryParse(value, ignoreCase: true, out result)
            || !Enum.IsDefined(result)
            || IsNumericLiteral(value)
        )
        {
            result = default;
            return false;
        }

        return true;
    }

    private static MemorySensitivity ParseSensitivityOrThrow(string value) =>
        TryParseSensitivity(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"BUG: sensitivity '{value}' must have been validated before parse."
            );

    private static MemoryStatus ParseStatusOrThrow(string value) =>
        TryParseStatus(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"BUG: status '{value}' must have been validated before parse."
            );

    /// <summary>
    /// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>
    /// happily parses <c>"1"</c> into the corresponding enum member.
    /// V0 rejects numeric literals so the API contract stays decoupled
    /// from the underlying integer encoding.
    /// </summary>
    private static bool IsNumericLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var i = value[0] is '+' or '-' ? 1 : 0;
        if (i >= value.Length)
        {
            return false;
        }

        for (; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                return false;
            }
        }
        return true;
    }
}
