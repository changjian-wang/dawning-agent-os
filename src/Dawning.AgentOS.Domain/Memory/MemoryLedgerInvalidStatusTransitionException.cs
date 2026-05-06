namespace Dawning.AgentOS.Domain.Memory;

/// <summary>
/// Thrown by <see cref="MemoryLedgerEntry"/> when a state-machine
/// transition is attempted from a status that does not allow it
/// (e.g. <see cref="MemoryLedgerEntry.UpdateContent"/> on a
/// <see cref="MemoryStatus.SoftDeleted"/> entry, or
/// <see cref="MemoryLedgerEntry.Restore"/> on an entry that is not
/// already soft-deleted).
/// </summary>
/// <remarks>
/// Per ADR-033 §决策 J1 the Application layer catches this exception
/// and surfaces it as the <c>memory.invalidStatusTransition</c>
/// error code, which the API layer maps to HTTP 422.
/// </remarks>
public sealed class MemoryLedgerInvalidStatusTransitionException : InvalidOperationException
{
    /// <summary>
    /// Creates a new exception describing the illegal transition.
    /// </summary>
    /// <param name="currentStatus">The status the aggregate was in.</param>
    /// <param name="attemptedAction">
    /// Short identifier of the attempted state-machine action
    /// (e.g. <c>"UpdateContent"</c>, <c>"Restore"</c>, <c>"Archive"</c>).
    /// </param>
    public MemoryLedgerInvalidStatusTransitionException(
        MemoryStatus currentStatus,
        string attemptedAction
    )
        : base(
            $"Memory ledger entry in status '{currentStatus}' does not allow action '{attemptedAction}'."
        )
    {
        CurrentStatus = currentStatus;
        AttemptedAction = attemptedAction;
    }

    /// <summary>The status the aggregate was in when the action was attempted.</summary>
    public MemoryStatus CurrentStatus { get; }

    /// <summary>The action that was attempted (e.g. <c>"UpdateContent"</c>).</summary>
    public string AttemptedAction { get; }
}
