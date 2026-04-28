using System.Net;
using System.Net.Http.Json;
using Dawning.AgentOS.Application.Contracts.Runtime;
using Dawning.AgentOS.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Dawning.AgentOS.Api.Tests;

public sealed class HealthEndpointTests
{
  [Fact]
  public async Task Health_requires_startup_token()
  {
    await using var factory = new LocalApiFactory();
    var client = factory.CreateClient();

    var response = await client.GetAsync("/health");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Health_returns_contract_when_startup_token_is_valid()
  {
    await using var factory = new LocalApiFactory();
    var client = factory.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
    request.Headers.Add(StartupTokenDefaults.HeaderName, LocalApiFactory.Token);

    var response = await client.SendAsync(request);

    response.EnsureSuccessStatusCode();
    var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

    Assert.NotNull(health);
    Assert.Equal("healthy", health.Status);
  }

  private sealed class LocalApiFactory : WebApplicationFactory<Program>
  {
    public const string Token = "test-token";

    private readonly string _dataDirectory = Path.Combine(
      Path.GetTempPath(),
      "DawningAgentOS.Api.Tests",
      Guid.NewGuid().ToString("N")
    );

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.ConfigureAppConfiguration(
        (_, configurationBuilder) =>
        {
          configurationBuilder.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
              ["Storage:DataDirectory"] = _dataDirectory,
              ["StartupToken:Token"] = Token,
            }
          );
        }
      );
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      SqliteConnection.ClearAllPools();

      if (Directory.Exists(_dataDirectory))
      {
        Directory.Delete(_dataDirectory, recursive: true);
      }
    }
  }
}
