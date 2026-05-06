using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.AgentOS.Api.Tests.Helpers;

/// <summary>
/// Integration-test factory that boots the API host in-memory. Replaces
/// <see cref="IClock"/> and <see cref="IRuntimeStartTimeProvider"/> with
/// deterministic fakes per ADR-023 §9 so tests are not coupled to wall
/// clock; per ADR-024 §J1 it also wires <see cref="SqliteOptions"/> to a
/// shared per-instance in-memory SQLite database so the schema bootstrap
/// runs against a fresh, hermetic store on each fixture.
/// </summary>
internal sealed class DawningAgentOsApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fixed UTC instant the fake start-time provider returns.</summary>
    public static DateTimeOffset StartedAtUtc { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The fixed UTC instant the fake clock returns.</summary>
    public static DateTimeOffset NowUtc { get; } = StartedAtUtc.AddMinutes(5);

    /// <summary>The startup token the factory configures the host to expect.</summary>
    public const string ExpectedToken = "test-token";

    /// <summary>
    /// Per-instance shared in-memory SQLite connection string. Each
    /// factory instance gets its own database, but the schema initializer
    /// and AppService probe see the same store within one fixture.
    /// </summary>
    private readonly string _sqliteConnectionString =
        $"Data Source=apitest-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    /// <summary>
    /// Keep-alive connection holding the in-memory SQLite database open
    /// for the lifetime of the factory. SQLite drops a shared-cache
    /// in-memory database the moment the last connection closes; without
    /// this anchor the schema initializer's migrations would be wiped
    /// before the AppService probe runs.
    /// </summary>
    private readonly SqliteConnection _keepAliveConnection;

    public DawningAgentOsApiFactory()
    {
        _keepAliveConnection = new SqliteConnection(_sqliteConnectionString);
        _keepAliveConnection.Open();
    }

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

            services.Configure<SqliteOptions>(o => o.DatabasePath = _sqliteConnectionString);
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
        base.Dispose(disposing);
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
