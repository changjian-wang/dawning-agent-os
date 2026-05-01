using Dawning.AgentOS.Api.DependencyInjection;
using Dawning.AgentOS.Api.Endpoints.Runtime;
using Dawning.AgentOS.Api.Middleware;
using Dawning.AgentOS.Application.DependencyInjection;
using Dawning.AgentOS.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ADR-023 §6 — composition root order is fixed:
// AddApplication() → AddInfrastructure() → AddApi(configuration).
builder.Services.AddApplication().AddInfrastructure().AddApi(builder.Configuration);

var app = builder.Build();

// ADR-023 §8 — middleware order:
// UseExceptionHandler → UseStatusCodePages → StartupTokenMiddleware →
// UseRouting → endpoints. The token check runs before routing so no
// endpoint executes for an unauthorized caller.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<StartupTokenMiddleware>();
app.UseRouting();

// ADR-023 §2 — endpoints are mapped via per-feature static classes.
app.MapRuntimeEndpoints();

app.Run();

// Required for WebApplicationFactory<Program> reflection lookup in
// integration tests (ADR-023 §9). Top-level statements emit Program into
// the global namespace, so the partial declaration must also stay there.
public partial class Program;
