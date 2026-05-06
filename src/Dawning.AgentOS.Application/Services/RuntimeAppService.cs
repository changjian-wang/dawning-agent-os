using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Abstractions.Hosting;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Application.Interfaces;
using Dawning.AgentOS.Application.Runtime;
using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Application.Services;

/// <summary>
/// Default implementation of <see cref="IRuntimeAppService"/>. Composes the
/// runtime start-time provider with the clock and probes the V0 SQLite
/// store via <see cref="IDbConnectionFactory"/> +
/// <see cref="IAppDataPathProvider"/> to populate the
/// <see cref="DatabaseStatus"/> tail of the snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-022 this service is the canonical reference for the AppService
/// facade pattern: API controllers depend on <see cref="IRuntimeAppService"/>
/// (declared in <c>Application/Interfaces</c>) rather than dispatching
/// messages through a mediator. Cross-cutting concerns (logging,
/// transaction, validation) are handled inside the AppService method body
/// or via decorators, never via pipeline behaviors.
/// </para>
/// <para>
/// Per ADR-024 §H1 / §4 the database probe never throws: any exception
/// from <see cref="IDbConnectionFactory.OpenAsync"/> or the schema-version
/// scalar query is caught and rendered as <see cref="DatabaseStatus"/>
/// with <c>Ready = false</c>. The endpoint must always succeed so the
/// desktop shell can poll status during an "initializing" UI state
/// instead of receiving a 5xx.
/// </para>
/// </remarks>
public sealed class RuntimeAppService(
    IClock clock,
    IRuntimeStartTimeProvider startTimeProvider,
    IDbConnectionFactory dbConnectionFactory,
    IAppDataPathProvider appDataPathProvider
) : IRuntimeAppService
{
    private const string SchemaVersionQuery = "SELECT MAX(version) FROM __schema_version";

    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IRuntimeStartTimeProvider _startTimeProvider =
        startTimeProvider ?? throw new ArgumentNullException(nameof(startTimeProvider));
    private readonly IDbConnectionFactory _dbConnectionFactory =
        dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
    private readonly IAppDataPathProvider _appDataPathProvider =
        appDataPathProvider ?? throw new ArgumentNullException(nameof(appDataPathProvider));

    /// <inheritdoc />
    public async Task<Result<RuntimeStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var startedAt = _startTimeProvider.StartedAtUtc;
        var now = _clock.UtcNow;
        var uptime = now >= startedAt ? now - startedAt : TimeSpan.Zero;

        var database = await ProbeDatabaseAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = new RuntimeStatus(
            StartedAtUtc: startedAt,
            NowUtc: now,
            Uptime: uptime,
            Healthy: true,
            Database: database
        );

        return Result<RuntimeStatus>.Success(snapshot);
    }

    private async Task<DatabaseStatus> ProbeDatabaseAsync(CancellationToken cancellationToken)
    {
        // Path resolution itself can fail (e.g. read-only home directory);
        // we still want to report Ready=false rather than throw, so the
        // status endpoint stays a 200.
        string? filePath;
        try
        {
            filePath = _appDataPathProvider.GetDatabasePath();
        }
        catch
        {
            return new DatabaseStatus(Ready: false, SchemaVersion: null, FilePath: null);
        }

        try
        {
            await using var connection = await _dbConnectionFactory
                .OpenAsync(cancellationToken)
                .ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = SchemaVersionQuery;

            var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var schemaVersion = raw is long v ? v : (long?)null;

            return new DatabaseStatus(
                Ready: true,
                SchemaVersion: schemaVersion,
                FilePath: filePath
            );
        }
        catch
        {
            return new DatabaseStatus(Ready: false, SchemaVersion: null, FilePath: filePath);
        }
    }
}
