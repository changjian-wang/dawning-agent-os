using Dawning.AgentOS.Application.Abstractions.Persistence;

namespace Dawning.AgentOS.Infrastructure.Persistence.Sqlite;

public sealed class PassthroughApplicationUnitOfWork : IApplicationUnitOfWork
{
  public async Task ExecuteAsync(
    Func<CancellationToken, Task> operation,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentNullException.ThrowIfNull(operation);
    await operation(cancellationToken);
  }

  public async Task<TResult> ExecuteAsync<TResult>(
    Func<CancellationToken, Task<TResult>> operation,
    CancellationToken cancellationToken = default
  )
  {
    ArgumentNullException.ThrowIfNull(operation);
    return await operation(cancellationToken);
  }
}
