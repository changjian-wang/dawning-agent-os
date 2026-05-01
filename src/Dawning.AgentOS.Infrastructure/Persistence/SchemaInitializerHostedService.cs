using Dawning.AgentOS.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dawning.AgentOS.Infrastructure.Persistence;

/// <summary>
/// Hosted service that runs <see cref="ISchemaInitializer"/> exactly
/// once during host startup, before <c>app.Run()</c> begins accepting
/// requests. Per ADR-024 §D1 schema bootstrap is fail-fast: a failure
/// here propagates out of <see cref="StartAsync"/> and prevents the
/// host from starting, surfacing the issue immediately rather than at
/// first-request time.
/// </summary>
/// <remarks>
/// <see cref="ISchemaInitializer"/> is registered with a scoped
/// lifetime (because it depends on the scoped
/// <see cref="IDbConnectionFactory"/>); this hosted service therefore
/// owns an <see cref="IServiceScope"/> for the duration of the
/// bootstrap call.
/// </remarks>
public sealed class SchemaInitializerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SchemaInitializerHostedService> logger
) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger<SchemaInitializerHostedService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running V0 SQLite schema bootstrap.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ISchemaInitializer>();
        await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("V0 SQLite schema bootstrap complete.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
