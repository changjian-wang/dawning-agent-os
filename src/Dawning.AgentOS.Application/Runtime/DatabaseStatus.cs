namespace Dawning.AgentOS.Application.Runtime;

/// <summary>
/// Reports the V0 SQLite store's readiness as observed by the
/// <c>RuntimeAppService</c>. Per ADR-024 §H1 this snapshot is the
/// runtime-observable smoke signal that the persistence stack came up;
/// it is intentionally surfaced through <see cref="RuntimeStatus"/>
/// rather than as a separate <c>/api/runtime/db-ping</c> endpoint.
/// </summary>
/// <param name="Ready">
/// <c>true</c> when the AppService successfully opened a connection and
/// read the <c>__schema_version</c> tracking table; <c>false</c> when
/// the connection threw or the table is unreadable. The runtime endpoint
/// always returns HTTP 200 even when <c>Ready</c> is <c>false</c>, so
/// the desktop shell can render a "database initializing" splash state
/// rather than a 5xx error.
/// </param>
/// <param name="SchemaVersion">
/// The highest <c>version</c> currently applied to the local store, or
/// <c>null</c> when the database is not ready or the table is empty.
/// </param>
/// <param name="FilePath">
/// The absolute filesystem path of the SQLite database file as resolved
/// by <c>IAppDataPathProvider</c>, or <c>null</c> when the path provider
/// is not consulted (e.g. when an in-memory test override is active).
/// </param>
public sealed record DatabaseStatus(bool Ready, long? SchemaVersion, string? FilePath);
