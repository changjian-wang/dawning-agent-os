using Dawning.AgentOS.Domain.Runtime;

namespace Dawning.AgentOS.Domain.Repositories;

public interface IRuntimeCheckpointRepository
{
  Task AddAsync(RuntimeCheckpoint checkpoint, CancellationToken cancellationToken = default);

  Task<RuntimeCheckpoint?> GetAsync(Guid id, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<RuntimeCheckpoint>> ListAsync(
    int page,
    int pageSize,
    CancellationToken cancellationToken = default
  );

  Task UpdateAsync(RuntimeCheckpoint checkpoint, CancellationToken cancellationToken = default);

  Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
