using Dawning.ORM.Dapper;

namespace Dawning.AgentOS.Infrastructure.Persistence.Dapper;

[Table("runtime_checkpoints")]
public sealed class RuntimeCheckpointRow
{
  [ExplicitKey]
  [Column("id")]
  public string Id { get; set; } = string.Empty;

  [Column("name")]
  public string Name { get; set; } = string.Empty;

  [Column("value")]
  public string Value { get; set; } = string.Empty;

  [Column("timestamp")]
  public long Timestamp { get; set; }

  [Column("created_at")]
  public string CreatedAt { get; set; } = string.Empty;

  [Column("updated_at")]
  public string UpdatedAt { get; set; } = string.Empty;
}
