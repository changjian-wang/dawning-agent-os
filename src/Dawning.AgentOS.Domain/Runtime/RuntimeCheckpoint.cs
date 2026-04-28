using Dawning.AgentOS.Domain.Common;

namespace Dawning.AgentOS.Domain.Runtime;

public sealed class RuntimeCheckpoint : Entity
{
  private RuntimeCheckpoint(
    Guid id,
    string name,
    string value,
    long timestamp,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt
  )
    : base(id, timestamp, createdAt)
  {
    Name = name;
    Value = value;
    UpdatedAt = updatedAt;
  }

  public string Name { get; private set; }

  public string Value { get; private set; }

  public DateTimeOffset UpdatedAt { get; private set; }

  public static RuntimeCheckpoint Create(string name, string value, DateTimeOffset createdAt)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(value);

    return new RuntimeCheckpoint(
      Guid.NewGuid(),
      name,
      value,
      createdAt.ToUnixTimeMilliseconds(),
      createdAt,
      createdAt
    );
  }

  public static RuntimeCheckpoint Rehydrate(
    Guid id,
    string name,
    string value,
    long timestamp,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt
  )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(value);

    return new RuntimeCheckpoint(id, name, value, timestamp, createdAt, updatedAt);
  }

  public void UpdateValue(string value, DateTimeOffset updatedAt)
  {
    ArgumentNullException.ThrowIfNull(value);

    Value = value;
    UpdatedAt = updatedAt;
    Timestamp = updatedAt.ToUnixTimeMilliseconds();
  }
}
