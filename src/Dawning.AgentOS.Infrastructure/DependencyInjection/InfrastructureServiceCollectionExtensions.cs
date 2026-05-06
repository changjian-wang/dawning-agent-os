using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Abstractions.Hosting;
using Dawning.AgentOS.Application.Abstractions.Llm;
using Dawning.AgentOS.Application.Abstractions.Persistence;
using Dawning.AgentOS.Application.Llm;
using Dawning.AgentOS.Domain.Chat;
using Dawning.AgentOS.Domain.Inbox;
using Dawning.AgentOS.Infrastructure.Hosting;
using Dawning.AgentOS.Infrastructure.Llm.AzureOpenAi;
using Dawning.AgentOS.Infrastructure.Llm.DeepSeek;
using Dawning.AgentOS.Infrastructure.Llm.OpenAi;
using Dawning.AgentOS.Infrastructure.Options;
using Dawning.AgentOS.Infrastructure.Persistence;
using Dawning.AgentOS.Infrastructure.Persistence.Chat;
using Dawning.AgentOS.Infrastructure.Persistence.Inbox;
using Dawning.AgentOS.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.DependencyInjection;

/// <summary>
/// Composition-root extension for the Infrastructure layer. Per ADR-023
/// §6 the API host chains <c>AddApplication() → AddInfrastructure() →
/// AddApi()</c>; this method registers concrete adapters for the ports
/// declared in <c>Application/Abstractions/</c>.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure-layer services on
    /// <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">The host configuration; used to bind <see cref="LlmOptions"/> per ADR-028 §决策 E1 / F1.</param>
    /// <returns>The same collection for fluent chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // ADR-026 §5: Dapper materializes snake_case columns into
        // PascalCase row classes; this is a process-wide setting and
        // is idempotent across calls.
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Time ports (ADR-017 / ADR-022).
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRuntimeStartTimeProvider, ProcessStartRuntimeStartTimeProvider>();

        // Persistence ports (ADR-024 §5). The path provider is a
        // singleton because the per-platform path never changes during
        // a process lifetime; the connection factory and schema
        // initializer are scoped so that cancellation tokens flow
        // naturally with the request scope (and so the hosted-service
        // bootstrap can build its own scope explicitly).
        services.AddSingleton<IAppDataPathProvider, AppDataPathProvider>();
        services.AddOptions<SqliteOptions>();
        services.AddScoped<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddScoped<ISchemaInitializer, SqliteSchemaInitializer>();
        services.AddHostedService<SchemaInitializerHostedService>();

        // Aggregate repositories (ADR-026 §5). The repository is scoped
        // alongside the connection factory; per-call connection lifetime
        // (ADR-024 §E1) keeps the repository safely re-entrant.
        services.AddScoped<IInboxRepository, InboxRepository>();
        // ADR-032 §决策 D2: chat session repository is scoped, mirrors
        // the inbox repository's lifetime — connection-per-call.
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();

        // LLM provider (ADR-028).
        services.AddLlm(configuration);

        return services;
    }

    /// <summary>
    /// Internal helper isolating the LLM-provider wiring fixed in
    /// ADR-028 §决策 A1 / E1 / F1 / G2 / I1: bind <see cref="LlmOptions"/>
    /// from configuration, validate <see cref="LlmOptions.ActiveProvider"/>
    /// at startup, register one named <see cref="HttpClient"/> per
    /// provider, and register exactly one <see cref="ILlmProvider"/>
    /// implementation (the active one) so callers stay agnostic.
    /// </summary>
    private static IServiceCollection AddLlm(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();

        // Resolve the bound options once at composition time so we can
        // pin each named HttpClient's BaseAddress correctly. ApiKey can
        // still be empty at this point — see ADR-028 §决策 G2.
        var snapshot = new LlmOptions();
        configuration.GetSection(LlmOptions.SectionName).Bind(snapshot);

        services.AddHttpClient(
            OpenAiLlmProvider.HttpClientName,
            client =>
            {
                if (!string.IsNullOrWhiteSpace(snapshot.Providers.OpenAI.BaseUrl))
                {
                    client.BaseAddress = new Uri(snapshot.Providers.OpenAI.BaseUrl);
                }
            }
        );

        services.AddHttpClient(
            DeepSeekLlmProvider.HttpClientName,
            client =>
            {
                if (!string.IsNullOrWhiteSpace(snapshot.Providers.DeepSeek.BaseUrl))
                {
                    client.BaseAddress = new Uri(snapshot.Providers.DeepSeek.BaseUrl);
                }
            }
        );

        services.AddHttpClient(
            AzureOpenAiLlmProvider.HttpClientName,
            client =>
            {
                // Azure OpenAI URL construction: endpoint + /openai/deployments/{id}
                if (
                    !string.IsNullOrWhiteSpace(snapshot.Providers.AzureOpenAI.Endpoint)
                    && !string.IsNullOrWhiteSpace(snapshot.Providers.AzureOpenAI.DeploymentId)
                )
                {
                    var endpoint = snapshot.Providers.AzureOpenAI.Endpoint.TrimEnd('/');
                    client.BaseAddress = new Uri(endpoint);
                }
            }
        );

        // Register exactly one ILlmProvider per the active selection.
        // Callers receive an opaque ILlmProvider; provider-specific
        // types (OpenAiLlmProvider / DeepSeekLlmProvider) are internal.
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<LlmOptions>>().CurrentValue;
            var logger = sp.GetRequiredService<ILogger<LlmOptions>>();
            var provider = ResolveActiveProvider(sp, opts);

            // ADR-028 §决策 G2 / ADR-029: empty ApiKey / Endpoint / DeploymentId are allowed at startup so
            // the rest of the surface remains usable; we surface a
            // single warning here so operators see the cause without
            // reading the source.
            if (
                string.Equals(
                    opts.ActiveProvider,
                    LlmOptions.AzureOpenAiProviderName,
                    StringComparison.Ordinal
                )
            )
            {
                var azureOpts = opts.Providers.AzureOpenAI;
                if (
                    string.IsNullOrEmpty(azureOpts.ApiKey)
                    || string.IsNullOrEmpty(azureOpts.Endpoint)
                    || string.IsNullOrEmpty(azureOpts.DeploymentId)
                )
                {
                    logger.LogWarning(
                        "LLM configuration for active provider 'AzureOpenAI' is incomplete. "
                            + "Set via environment variables (LLM_PROVIDERS_AZUREOPENAI_APIKEY, "
                            + "LLM_PROVIDERS_AZUREOPENAI_ENDPOINT, LLM_PROVIDERS_AZUREOPENAI_DEPLOYMENTID) "
                            + "or dotnet user-secrets. Until configured, LLM calls will fail."
                    );
                }
            }
            else
            {
                var providerOptions = string.Equals(
                    opts.ActiveProvider,
                    LlmOptions.OpenAiProviderName,
                    StringComparison.Ordinal
                )
                    ? opts.Providers.OpenAI
                    : opts.Providers.DeepSeek;

                if (string.IsNullOrEmpty(providerOptions.ApiKey))
                {
                    logger.LogWarning(
                        "LLM ApiKey for active provider '{Provider}' is empty. Set via environment variable "
                            + "(e.g., LLM_PROVIDERS_OPENAI_APIKEY for OpenAI) or dotnet user-secrets. "
                            + "Until configured, LLM calls will return llm.authenticationFailed.",
                        opts.ActiveProvider
                    );
                }
            }

            return provider;
        });

        return services;
    }

    private static ILlmProvider ResolveActiveProvider(IServiceProvider sp, LlmOptions options)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<LlmOptions>>();

        if (
            string.Equals(
                options.ActiveProvider,
                LlmOptions.OpenAiProviderName,
                StringComparison.Ordinal
            )
        )
        {
            return new OpenAiLlmProvider(httpClientFactory, optionsMonitor);
        }

        if (
            string.Equals(
                options.ActiveProvider,
                LlmOptions.DeepSeekProviderName,
                StringComparison.Ordinal
            )
        )
        {
            return new DeepSeekLlmProvider(httpClientFactory, optionsMonitor);
        }

        if (
            string.Equals(
                options.ActiveProvider,
                LlmOptions.AzureOpenAiProviderName,
                StringComparison.Ordinal
            )
        )
        {
            return new AzureOpenAiLlmProvider(httpClientFactory, optionsMonitor);
        }

        // The IValidateOptions<LlmOptions> guard runs at startup before
        // this factory is invoked, so reaching here would indicate a
        // bypassed validation chain. Throwing matches the existing
        // ArgumentException convention in this layer.
        throw new InvalidOperationException(
            $"Unknown LLM ActiveProvider '{options.ActiveProvider}'; expected "
                + $"'{LlmOptions.OpenAiProviderName}', '{LlmOptions.DeepSeekProviderName}', or '{LlmOptions.AzureOpenAiProviderName}'."
        );
    }
}
