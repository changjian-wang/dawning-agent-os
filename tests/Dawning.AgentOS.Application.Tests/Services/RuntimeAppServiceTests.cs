using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Runtime;
using Dawning.AgentOS.Application.Services;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RuntimeAppService"/>. Per ADR-022 the AppService
/// is the canonical Application layer entry point; these tests cover the
/// happy path, clock-skew clamping, and constructor null-guards.
/// </summary>
[TestFixture]
public class RuntimeAppServiceTests
{
    [Test]
    public async Task GetStatusAsync_ReturnsHealthySnapshotWithComputedUptime()
    {
        var startedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddMinutes(7);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(now);

        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        startProvider.SetupGet(s => s.StartedAtUtc).Returns(startedAt);

        IRuntimeAppService sut = new RuntimeAppService(clock.Object, startProvider.Object);

        var result = await sut.GetStatusAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        var snapshot = result.Value;
        Assert.That(snapshot.StartedAtUtc, Is.EqualTo(startedAt));
        Assert.That(snapshot.NowUtc, Is.EqualTo(now));
        Assert.That(snapshot.Uptime, Is.EqualTo(TimeSpan.FromMinutes(7)));
        Assert.That(snapshot.Healthy, Is.True);
    }

    [Test]
    public async Task GetStatusAsync_ClampsUptimeToZeroWhenClockSkewMakesNowPrecedeStart()
    {
        var startedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        // Simulate an OS clock that jumped backwards by 30 seconds.
        var now = startedAt.AddSeconds(-30);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(now);

        var startProvider = new Mock<IRuntimeStartTimeProvider>();
        startProvider.SetupGet(s => s.StartedAtUtc).Returns(startedAt);

        var sut = new RuntimeAppService(clock.Object, startProvider.Object);

        var result = await sut.GetStatusAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Uptime, Is.EqualTo(TimeSpan.Zero));
        Assert.That(result.Value.NowUtc, Is.EqualTo(now));
    }

    [Test]
    public void Constructor_ThrowsWhenClockIsNull()
    {
        var startProvider = new Mock<IRuntimeStartTimeProvider>();

        Assert.That(
            () => new RuntimeAppService(null!, startProvider.Object),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("clock")
        );
    }

    [Test]
    public void Constructor_ThrowsWhenStartTimeProviderIsNull()
    {
        var clock = new Mock<IClock>();

        Assert.That(
            () => new RuntimeAppService(clock.Object, null!),
            Throws
                .TypeOf<ArgumentNullException>()
                .With.Property("ParamName")
                .EqualTo("startTimeProvider")
        );
    }
}
