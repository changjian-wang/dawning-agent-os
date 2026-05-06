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
/// <remarks>
/// The injected <see cref="IClock"/> is monotonic, not strictly fixed:
/// <see cref="NowUtc"/> is the floor instant on the first read, and each
/// subsequent <see cref="IClock.UtcNow"/> advances by 1 millisecond. A
/// strictly fixed clock collapses every UUIDv7 generated within the
/// test into the same millisecond bucket, which then forces sort
/// order to fall back to the random tail — the chat £ssertion
/// <c>messages[0].Role == "user"</c> would flake ≈37% of the time
/// because <c>created_at_utc ASC, id ASC</c> could not break the
/// tie deterministically. Advancing by 1ms per call removes that
/// degeneracy without polluting tests with real wall time.
/// Assertions that compared a persisted timestamp to <see cref="NowUtc"/>
/// must therefore allow a small forward drift (≤1 second is plenty for
/// any single fixture).
/// </remarks>
internal sealed class DawningAgentOsApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fixed UTC instant the fake start-time provider returns.</summary>
    public static DateTimeOffset StartedAtUtc { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The base UTC instant the fake clock returns on its first read.
    /// Subsequent reads advance by 1 millisecond each so UUIDv7
    /// identifiers generated within a single fixture remain strictly
    /// monotonic; tests that compare a persisted timestamp to this
    /// constant should allow up to <see cref="MaxClockDrift"/> forward
    /// drift.
    /// </summary>
    public static DateTimeOffset NowUtc { get; } = StartedAtUtc.AddMinutes(5);

    /// <summary>
    /// Generous upper bound on how far the monotonic test clock can
    /// drift past <see cref="NowUtc"/> within a single fixture; tests
    /// that previously asserted exact equality should switch to
    /// <c>.Within(MaxClockDrift)</c> tolerance.
    /// </summary>
    public static TimeSpan MaxClockDrift { get; } = TimeSpan.FromSeconds(1);

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

            services.AddSingleton<IClock>(new MonotonicTestClock(NowUtc));
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

    /// <summary>
    /// <see cref="IClock"/> stub that returns <paramref name="baseInstant"/>
    /// on the first read and advances by 1 millisecond for every
    /// subsequent read. The advance is the smallest amount that still
    /// gives <see cref="Guid.CreateVersion7(DateTimeOffset)"/> a fresh
    /// millisecond bucket per call, which is what UUIDv7 needs to keep
    /// sort-order monotonicity. Drift is bounded by
    /// <see cref="DawningAgentOsApiFactory.MaxClockDrift"/> for any
    /// realistic fixture, so equality assertions should switch to
    /// <c>.Within(MaxClockDrift)</c>.
    /// </summary>
    private sealed class MonotonicTestClock(DateTimeOffset baseInstant) : IClock
    {
        private readonly DateTimeOffset _baseInstant = baseInstant;
        private long _calls = -1;

        public DateTimeOffset UtcNow =>
            _baseInstant.AddMilliseconds(Interlocked.Increment(ref _calls));
    }

    private sealed class FixedRuntimeStartTimeProvider(DateTimeOffset value)
        : IRuntimeStartTimeProvider
    {
        public DateTimeOffset StartedAtUtc { get; } = value;
    }
}
