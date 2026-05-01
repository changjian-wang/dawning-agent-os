using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Dawning.AgentOS.Infrastructure.Persistence;

/// <summary>
/// V0 implementation of <see cref="ISchemaInitializer"/>: applies the
/// embedded <c>Persistence/Migrations/NNNN_*.sql</c> resources in
/// monotonic order and records each successful application in the
/// <c>__schema_version</c> table per ADR-024 §2 / §3 / §I1.
/// </summary>
/// <remarks>
/// <para>
/// The seeder migration <c>0001_init_schema_version.sql</c> creates the
/// tracking table itself; that migration is the only one allowed to run
/// without first checking <c>__schema_version</c>. From <c>0002</c>
/// onwards every file is skipped if its version is already in the
/// tracking table, making
/// <see cref="InitializeAsync(CancellationToken)"/> idempotent across
/// process restarts.
/// </para>
/// <para>
/// Each migration is wrapped in a single transaction; a failure rolls
/// back the SQL changes <em>and</em> the corresponding
/// <c>__schema_version</c> insert, so a half-applied state cannot be
/// recorded as "applied".
/// </para>
/// </remarks>
public sealed partial class SqliteSchemaInitializer(
    IDbConnectionFactory connectionFactory,
    ILogger<SqliteSchemaInitializer> logger
) : ISchemaInitializer
{
    private const string MigrationResourcePrefix =
        "Dawning.AgentOS.Infrastructure.Persistence.Migrations.";

    private const string MigrationResourceSuffix = ".sql";

    private const string EnsureSchemaVersionTableSql =
        "CREATE TABLE IF NOT EXISTS __schema_version ("
        + "version INTEGER NOT NULL PRIMARY KEY, "
        + "applied_at TEXT NOT NULL"
        + ");";

    private const string SelectAppliedVersionsSql =
        "SELECT version FROM __schema_version ORDER BY version ASC";

    private const string InsertAppliedVersionSql =
        "INSERT INTO __schema_version (version, applied_at) VALUES ($version, $applied_at)";

    private readonly IDbConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly ILogger<SqliteSchemaInitializer> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        // Bootstrap: make sure the tracking table exists before we read
        // it. CREATE TABLE IF NOT EXISTS is idempotent so this is safe
        // to issue on every startup, including the very first run.
        await using (var bootstrap = connection.CreateCommand())
        {
            bootstrap.CommandText = EnsureSchemaVersionTableSql;
            await bootstrap.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var applied = await ReadAppliedVersionsAsync(connection, cancellationToken)
            .ConfigureAwait(false);

        var migrations = DiscoverMigrations();

        foreach (var migration in migrations)
        {
            if (applied.Contains(migration.Version))
            {
                continue;
            }

            await ApplyMigrationAsync(connection, migration, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Applied schema migration {Version}: {Name}",
                migration.Version,
                migration.Name
            );
        }
    }

    private static async Task<HashSet<long>> ReadAppliedVersionsAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken
    )
    {
        var versions = new HashSet<long>();

        await using var command = connection.CreateCommand();
        command.CommandText = SelectAppliedVersionsSql;

        await using var reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(reader.GetInt64(0));
        }

        return versions;
    }

    private static IReadOnlyList<DiscoveredMigration> DiscoverMigrations()
    {
        var assembly = typeof(SqliteSchemaInitializer).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        var pattern = MigrationFileNamePattern();
        var discovered = new List<DiscoveredMigration>();

        foreach (var resourceName in resourceNames)
        {
            if (
                !resourceName.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal)
                || !resourceName.EndsWith(MigrationResourceSuffix, StringComparison.Ordinal)
            )
            {
                continue;
            }

            var fileName = resourceName.Substring(
                MigrationResourcePrefix.Length,
                resourceName.Length
                    - MigrationResourcePrefix.Length
                    - MigrationResourceSuffix.Length
            );

            var match = pattern.Match(fileName);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"Embedded migration resource '{resourceName}' does not match the required NNNN_<name> filename convention."
                );
            }

            var version = long.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);
            var name = match.Groups["name"].Value;

            discovered.Add(new DiscoveredMigration(version, name, resourceName, assembly));
        }

        return discovered.OrderBy(m => m.Version).ToArray();
    }

    private static async Task ApplyMigrationAsync(
        System.Data.Common.DbConnection connection,
        DiscoveredMigration migration,
        CancellationToken cancellationToken
    )
    {
        var sql = await migration.LoadSqlAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await using (var migrationCommand = connection.CreateCommand())
            {
                migrationCommand.Transaction = transaction;
                migrationCommand.CommandText = sql;
                await migrationCommand
                    .ExecuteNonQueryAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            // The seed migration (0001) creates the tracking table
            // itself; SQLite tolerates inserting into a table created
            // earlier in the same transaction, so the same code path
            // handles both seeding and ordinary migrations.
            await using (var trackCommand = connection.CreateCommand())
            {
                trackCommand.Transaction = transaction;
                trackCommand.CommandText = InsertAppliedVersionSql;

                var versionParameter = trackCommand.CreateParameter();
                versionParameter.ParameterName = "$version";
                versionParameter.Value = migration.Version;
                trackCommand.Parameters.Add(versionParameter);

                var appliedAtParameter = trackCommand.CreateParameter();
                appliedAtParameter.ParameterName = "$applied_at";
                appliedAtParameter.Value = DateTimeOffset.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture
                );
                trackCommand.Parameters.Add(appliedAtParameter);

                await trackCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    [GeneratedRegex(@"^(?<version>\d{4})_(?<name>[a-z0-9_]+)$")]
    private static partial Regex MigrationFileNamePattern();

    private sealed record DiscoveredMigration(
        long Version,
        string Name,
        string ResourceName,
        Assembly OwningAssembly
    )
    {
        public async Task<string> LoadSqlAsync(CancellationToken cancellationToken)
        {
            await using var stream =
                OwningAssembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded migration resource '{ResourceName}' could not be opened."
                );

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
