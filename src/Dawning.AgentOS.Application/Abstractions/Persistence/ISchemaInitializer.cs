namespace Dawning.AgentOS.Application.Abstractions.Persistence;

/// <summary>
/// Idempotent schema bootstrapper. Adapters implementing this port live
/// in <c>Dawning.AgentOS.Infrastructure</c>; per ADR-024 §2 the V0
/// implementation scans the embedded <c>Persistence/Migrations/*.sql</c>
/// resources and applies any not yet recorded in the
/// <c>__schema_version</c> tracking table.
/// </summary>
/// <remarks>
/// Ran once at host startup by
/// <c>SchemaInitializerHostedService</c>; subsequent starts re-run with
/// no side effects. The contract intentionally hides the migration
/// strategy so callers cannot depend on it (per ADR-024 §C1 we may swap
/// to DbUp / FluentMigrator if the migration count grows past 10).
/// </remarks>
public interface ISchemaInitializer
{
    /// <summary>
    /// Applies any pending schema migrations. Safe to call multiple
    /// times; the contract is idempotent.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A task that completes when the schema is fully applied.</returns>
    Task InitializeAsync(CancellationToken cancellationToken);
}
