namespace Dawning.AgentOS.Application.Abstractions.Persistence;

public interface IApplicationUnitOfWork
{
  Task ExecuteAsync(
    Func<CancellationToken, Task> operation,
    CancellationToken cancellationToken = default
  );

  Task<TResult> ExecuteAsync<TResult>(
    Func<CancellationToken, Task<TResult>> operation,
    CancellationToken cancellationToken = default
  );
}
