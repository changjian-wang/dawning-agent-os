using Dawning.AgentOS.Domain.Core;
using Dawning.AgentOS.Domain.Memory;

namespace Dawning.AgentOS.Application.Memory;

/// <summary>
/// Factory helpers for memory-side <see cref="DomainError"/> codes used
/// by the application layer. Per ADR-033 §决策 J1 the memory ledger
/// adds two non-field errors that need bespoke HTTP mapping in
/// <c>MemoryEndpoints</c>:
/// <list type="bullet">
///   <item>
///     <description><c>memory.notFound</c> → HTTP 404 (entry lookup miss);</description>
///   </item>
///   <item>
///     <description><c>memory.invalidStatusTransition</c> → HTTP 422
///     (state-machine refused the requested transition).</description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Field-level validation errors (<c>memory.content.required</c>,
/// <c>memory.scope.tooLong</c>, <c>memory.limit.outOfRange</c>, etc.)
/// are emitted inline by <c>MemoryLedgerAppService</c> rather than
/// threaded through this class — those are field-level errors that
/// the existing <c>ResultHttpExtensions.ToHttpResult</c> already maps
/// correctly to HTTP 400. This class only collects the non-field
/// errors that need bespoke HTTP mapping.
/// </remarks>
public static class MemoryErrors
{
    /// <summary>The error code returned when a memory entry lookup misses.</summary>
    public const string NotFoundCode = "memory.notFound";

    /// <summary>
    /// The error code returned when the requested PATCH would drive
    /// the aggregate through an illegal state-machine transition,
    /// per ADR-033 §决策 J1 (HTTP 422).
    /// </summary>
    public const string InvalidStatusTransitionCode = "memory.invalidStatusTransition";

    /// <summary>
    /// Error code returned when a PATCH body contains no fields
    /// (all optional fields null). HTTP 400.
    /// </summary>
    public const string UpdateEmptyCode = "memory.update.empty";

    /// <summary>
    /// Builds an <see cref="DomainError"/> indicating that no memory
    /// entry matches the supplied id. Per ADR-033 §决策 J1 the API
    /// layer maps this to HTTP 404 in <c>MemoryEndpoints</c>'s manual
    /// error switch.
    /// </summary>
    /// <param name="entryId">The id that produced the miss; included in the message for diagnostics.</param>
    public static DomainError NotFound(Guid entryId) =>
        new(Code: NotFoundCode, Message: $"Memory entry '{entryId}' not found.", Field: null);

    /// <summary>
    /// Builds a <see cref="DomainError"/> describing an illegal
    /// state-machine transition. The aggregate raised
    /// <see cref="MemoryLedgerInvalidStatusTransitionException"/> and
    /// the AppService translated it here.
    /// </summary>
    public static DomainError InvalidStatusTransition(
        MemoryStatus currentStatus,
        string attemptedAction
    ) =>
        new(
            Code: InvalidStatusTransitionCode,
            Message: $"Memory entry in status '{currentStatus}' does not allow action '{attemptedAction}'.",
            Field: null
        );

    /// <summary>
    /// Builds a field-level error for an empty PATCH payload.
    /// </summary>
    public static DomainError UpdateEmpty() =>
        new(
            Code: UpdateEmptyCode,
            Message: "PATCH body must contain at least one of: content, scope, sensitivity, status.",
            Field: null
        );
}
