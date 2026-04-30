using System.Collections.Generic;
using Dawning.AgentOS.Domain.Permissions;

namespace Dawning.AgentOS.Domain.Services.Permissions;

/// <summary>
/// Default <see cref="IActionClassifier"/> backed by a fixed V0 vocabulary.
/// </summary>
/// <remarks>
/// Unknown action codes throw rather than defaulting to a permissive level:
/// silently classifying an unknown action as L0 would let new actions ship
/// without an explicit risk decision.
/// </remarks>
public sealed class ActionClassifier : IActionClassifier
{
    private static readonly IReadOnlyDictionary<string, ActionLevel> Map = new Dictionary<string, ActionLevel>(
        StringComparer.Ordinal)
    {
        [ActionKind.ReadSummarize.Code] = ActionLevel.L0,
        [ActionKind.ReadClassify.Code] = ActionLevel.L0,
        [ActionKind.ReadTag.Code] = ActionLevel.L1,
        [ActionKind.InboxAdd.Code] = ActionLevel.L1,
        [ActionKind.MemoryWrite.Code] = ActionLevel.L2,
        [ActionKind.MemoryDelete.Code] = ActionLevel.L3,
    };

    /// <inheritdoc />
    public ActionLevel Classify(ActionKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        if (!Map.TryGetValue(kind.Code, out var level))
        {
            throw new ArgumentException(
                $"Unknown action kind '{kind.Code}'. Add it to the V0 vocabulary or extend the classifier.",
                nameof(kind));
        }

        return level;
    }
}
