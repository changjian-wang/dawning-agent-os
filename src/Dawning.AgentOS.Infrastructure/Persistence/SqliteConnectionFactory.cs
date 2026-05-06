using System.Data.Common;
using Dawning.AgentOS.Application.Abstractions.Hosting;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Infrastructure.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.Persistence;

/// <summary>
/// V0 implementation of <see cref="IDbConnectionFactory"/> backed by
/// <c>Microsoft.Data.Sqlite</c>. Per ADR-024 §2 / §E1 the factory
/// returns a fresh, opened <see cref="SqliteConnection"/> per call;
/// callers are responsible for disposing it.
/// </summary>
/// <remarks>
/// <para>
/// The connection string is built once per call from either the
/// <see cref="SqliteOptions.DatabasePath"/> override (test path) or the
/// per-platform <see cref="IAppDataPathProvider.GetDatabasePath()"/>
/// (production path). Foreign keys are enabled and journal mode is set
/// to WAL via <c>PRAGMA</c> after the connection opens; both PRAGMAs are
/// idempotent across connections.
/// </para>
/// <para>
/// The Options-override branch accepts either a bare file path or a
/// fully-formed connection string (the latter is detected by the
/// presence of <c>'='</c>). This lets tests inject
/// <c>"Data Source=test_xyz;Mode=Memory;Cache=Shared"</c> verbatim
/// without us hand-rolling a connection-string builder.
/// </para>
/// </remarks>
public sealed class SqliteConnectionFactory(
    IAppDataPathProvider appDataPathProvider,
    IOptions<SqliteOptions> options
) : IDbConnectionFactory
{
    private readonly IAppDataPathProvider _appDataPathProvider =
        appDataPathProvider ?? throw new ArgumentNullException(nameof(appDataPathProvider));
    private readonly IOptions<SqliteOptions> _options =
        options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = BuildConnectionString();
        var connection = new SqliteConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ApplyConnectionPragmasAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private string BuildConnectionString()
    {
        var overridePath = _options.Value.DatabasePath;
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath.Contains('=', StringComparison.Ordinal)
                ? overridePath
                : new SqliteConnectionStringBuilder
                {
                    DataSource = overridePath,
                    ForeignKeys = true,
                    Cache = SqliteCacheMode.Shared,
                }.ToString();
        }

        var path = _appDataPathProvider.GetDatabasePath();
        return new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    private static async Task ApplyConnectionPragmasAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken
    )
    {
        // ConnectionString-level ForeignKeys=true is honoured by
        // Microsoft.Data.Sqlite; the WAL pragma is a database-wide
        // setting that survives between connections, but issuing it
        // every open is harmless and keeps fresh test databases on the
        // same journal mode as production.
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
