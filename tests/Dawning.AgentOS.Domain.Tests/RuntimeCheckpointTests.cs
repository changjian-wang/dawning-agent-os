using Dawning.AgentOS.Domain.Runtime;

namespace Dawning.AgentOS.Domain.Tests;

public sealed class RuntimeCheckpointTests
{
  [Fact]
  public void Create_sets_identity_and_timestamp()
  {
    var createdAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z");

    var checkpoint = RuntimeCheckpoint.Create("storage-probe", "created", createdAt);

    Assert.NotEqual(Guid.Empty, checkpoint.Id);
    Assert.Equal("storage-probe", checkpoint.Name);
    Assert.Equal("created", checkpoint.Value);
    Assert.Equal(createdAt.ToUnixTimeMilliseconds(), checkpoint.Timestamp);
  }

  [Fact]
  public void UpdateValue_changes_value_and_timestamp()
  {
    var checkpoint = RuntimeCheckpoint.Create(
      "storage-probe",
      "created",
      DateTimeOffset.Parse("2026-04-28T00:00:00Z")
    );
    var updatedAt = DateTimeOffset.Parse("2026-04-28T01:00:00Z");

    checkpoint.UpdateValue("updated", updatedAt);

    Assert.Equal("updated", checkpoint.Value);
    Assert.Equal(updatedAt.ToUnixTimeMilliseconds(), checkpoint.Timestamp);
    Assert.Equal(updatedAt, checkpoint.UpdatedAt);
  }
}
