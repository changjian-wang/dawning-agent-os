using Dawning.AgentOS.Application.Abstractions;

namespace Dawning.AgentOS.Infrastructure.Time;

/// <summary>
/// Default <see cref="IClock"/> implementation backed by
/// <see cref="DateTimeOffset.UtcNow"/>. Registered as a singleton.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
