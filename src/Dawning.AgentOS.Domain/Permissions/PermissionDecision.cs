namespace Dawning.AgentOS.Domain.Permissions;

/// <summary>
/// Outcome of evaluating whether an agent action may proceed.
/// </summary>
/// <remarks>
/// Modeled as a closed hierarchy of records (Allowed / RequiresConfirmation /
/// Denied) so callers can pattern-match exhaustively and carry per-case data
/// (e.g. a confirmation prompt or a denial reason). All variants are sealed;
/// no further subclassing is supported.
/// </remarks>
public abstract record PermissionDecision
{
    private PermissionDecision() { }

    /// <summary>Action may proceed without user interaction.</summary>
    public sealed record Allowed : PermissionDecision
    {
        /// <summary>Singleton instance; the type carries no data.</summary>
        public static readonly Allowed Instance = new();

        private Allowed() { }
    }

    /// <summary>Action may proceed only after the user explicitly confirms.</summary>
    /// <param name="Reason">Short, human-readable rationale; surfaced in the confirmation prompt.</param>
    public sealed record RequiresConfirmation(string Reason) : PermissionDecision
    {
        /// <summary>Reason shown to the user; must be non-empty.</summary>
        public string Reason { get; } = ValidateReason(Reason);

        private static string ValidateReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason must be non-empty.", nameof(reason));
            }

            return reason;
        }
    }

    /// <summary>Action is refused; caller must not attempt it.</summary>
    /// <param name="Reason">Short, human-readable rationale; surfaced in error responses.</param>
    public sealed record Denied(string Reason) : PermissionDecision
    {
        /// <summary>Reason shown to the user; must be non-empty.</summary>
        public string Reason { get; } = ValidateReason(Reason);

        private static string ValidateReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason must be non-empty.", nameof(reason));
            }

            return reason;
        }
    }
}
