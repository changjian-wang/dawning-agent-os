using Dawning.AgentOS.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.AgentOS.Api.Tests.Helpers;

/// <summary>
/// Integration-test factory that boots the API host in-memory. Replaces
/// <see cref="IClock"/> and <see cref="IRuntimeStartTimeProvider"/> with
/// deterministic fakes per ADR-023 §9 so tests are not coupled to wall
/// clock.
/// </summary>
internal sealed class DawningAgentOsApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fixed UTC instant the fake start-time provider returns.</summary>
    public static DateTimeOffset StartedAtUtc { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The fixed UTC instant the fake clock returns.</summary>
    public static DateTimeOffset NowUtc { get; } = StartedAtUtc.AddMinutes(5);

    /// <summary>The startup token the factory configures the host to expect.</summary>
    public const string ExpectedToken = "test-token";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration(
            (_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["Api:StartupToken:HeaderName"] = "X-Startup-Token",
                        ["Api:StartupToken:ExpectedToken"] = ExpectedToken,
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.RemoveAll<IRuntimeStartTimeProvider>();

            services.AddSingleton<IClock>(new FixedClock(NowUtc));
            services.AddSingleton<IRuntimeStartTimeProvider>(
                new FixedRuntimeStartTimeProvider(StartedAtUtc)
            );
        });
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class FixedRuntimeStartTimeProvider(DateTimeOffset value)
        : IRuntimeStartTimeProvider
    {
        public DateTimeOffset StartedAtUtc { get; } = value;
    }
}
