namespace Dawning.AgentOS.Application.Abstractions.Hosting;

/// <summary>
/// Resolves the local filesystem path of the V0 SQLite database file.
/// Adapters implementing this port live in
/// <c>Dawning.AgentOS.Infrastructure</c>; per ADR-024 §B1 the V0
/// implementation routes to a per-platform application-data directory
/// (Windows <c>%LOCALAPPDATA%</c>, macOS
/// <c>~/Library/Application Support</c>, Linux <c>$XDG_DATA_HOME</c> or
/// <c>~/.local/share</c>).
/// </summary>
/// <remarks>
/// The port is intentionally narrow: a single absolute path. Future
/// extensions (cloud-sync mirror, encrypted store, multi-profile) get
/// modelled here without touching the Application layer.
/// </remarks>
public interface IAppDataPathProvider
{
    /// <summary>
    /// Returns the absolute path of the SQLite database file on the
    /// current platform. The containing directory is guaranteed to
    /// exist when this method returns (the implementation creates it
    /// if necessary); the file itself may not yet exist on first run.
    /// </summary>
    /// <returns>An absolute filesystem path.</returns>
    string GetDatabasePath();
}
