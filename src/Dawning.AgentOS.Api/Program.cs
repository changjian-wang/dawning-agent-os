using Dawning.AgentOS.Api.Endpoints;
using Dawning.AgentOS.Api.Middleware;
using Dawning.AgentOS.Application.DependencyInjection;
using Dawning.AgentOS.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddDomainServices();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<LocalExceptionHandlingMiddleware>();
app.UseMiddleware<StartupTokenMiddleware>();

app.MapHealthEndpoints();
app.MapRuntimeEndpoints();

await app.RunAsync();

public partial class Program;
