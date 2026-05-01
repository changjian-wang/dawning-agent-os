namespace Dawning.AgentOS.Infrastructure.Options;

/// <summary>
/// Options bag for the V0 SQLite adapter. Per ADR-024 §2 the production
/// path resolves the database location through
/// <c>IAppDataPathProvider</c>; <see cref="DatabasePath"/> here exists as
/// a deterministic override hook for tests
/// (<c>Mode=Memory;Cache=Shared</c>) and never appears in
/// <c>appsettings.json</c> shipped to end users.
/// </summary>
public sealed class SqliteOptions
{
    /// <summary>
    /// When non-null, used verbatim as the SQLite ADO.NET connection
    /// string instead of resolving the per-platform application-data
    /// path. Keep null in production; tests wire this to
    /// <c>"Data Source=test_&lt;guid&gt;;Mode=Memory;Cache=Shared"</c>.
    /// </summary>
    public string? DatabasePath { get; set; }
}
