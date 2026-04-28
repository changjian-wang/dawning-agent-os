namespace Dawning.AgentOS.Application.Contracts.Runtime;

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);
