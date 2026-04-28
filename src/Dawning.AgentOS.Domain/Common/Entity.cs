namespace Dawning.AgentOS.Domain.Common;

public abstract class Entity
{
  protected Entity(Guid id, long timestamp, DateTimeOffset createdAt)
  {
    if (id == Guid.Empty)
    {
      throw new ArgumentException("Entity id cannot be empty.", nameof(id));
    }

    Id = id;
    Timestamp = timestamp;
    CreatedAt = createdAt;
  }

  public Guid Id { get; }

  public long Timestamp { get; protected set; }

  public DateTimeOffset CreatedAt { get; }
}
