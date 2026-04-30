using Dawning.AgentOS.Domain.Permissions;

namespace Dawning.AgentOS.Domain.Services.Permissions;

/// <summary>
/// Maps an <see cref="ActionKind"/> to the risk level that determines whether
/// the agent self-decides, asks for confirmation, or refuses.
/// </summary>
/// <remarks>
/// Implementations are stateless and pure; the same input always yields the
/// same output within a single deployed version.
/// </remarks>
public interface IActionClassifier
{
    /// <summary>Classifies the given action kind.</summary>
    /// <exception cref="ArgumentNullException">When <paramref name="kind"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">When <paramref name="kind"/> is unknown to the classifier.</exception>
    ActionLevel Classify(ActionKind kind);
}
