using System.Data.Common;

namespace Dawning.AgentOS.Application.Abstractions.Persistence;

/// <summary>
/// Opens an ADO.NET connection to the V0 persistence store. Adapters
/// implementing this port live in <c>Dawning.AgentOS.Infrastructure</c>;
/// per ADR-024 §1 the V0 implementation backs onto a per-platform SQLite
/// file resolved through <see cref="Hosting.IAppDataPathProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// The port returns <see cref="DbConnection"/> (the BCL abstract base)
/// rather than <c>IDbConnection</c> so future Aggregate Repositories can
/// reach Dapper's full async surface (<c>QueryAsync</c>,
/// <c>ExecuteAsync</c>) without re-wrapping. <see cref="DbConnection"/>
/// is part of <c>System.Data.Common</c> in the BCL, so the Application
/// layer keeps its zero-third-party-runtime stance.
/// </para>
/// <para>
/// Ownership: the caller is responsible for disposing the returned
/// connection (typically via an <c>await using</c> block). Connections
/// are short-lived per ADR-024 §E1; long-lived singleton or scoped
/// connections were rejected.
/// </para>
/// </remarks>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Opens a new connection asynchronously. The connection is already
    /// in <see cref="System.Data.ConnectionState.Open"/> when this task
    /// completes successfully.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>An opened <see cref="DbConnection"/>.</returns>
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}
