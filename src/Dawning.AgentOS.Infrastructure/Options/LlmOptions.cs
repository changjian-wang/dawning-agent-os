using Microsoft.Extensions.Options;

namespace Dawning.AgentOS.Infrastructure.Options;

/// <summary>
/// Strongly-typed bind target for the <c>Llm</c> configuration section.
/// Per ADR-028 §决策 E1 / F1 / G2 V0 holds:
/// <list type="bullet">
///   <item>
///     <description>
///       a single <see cref="ActiveProvider"/> string that selects which
///       <c>ILlmProvider</c> implementation gets registered;
///     </description>
///   </item>
///   <item>
///     <description>
///       a per-provider sub-section with <c>ApiKey</c> / <c>BaseUrl</c> /
///       <c>Model</c>; ApiKey defaults to empty string and is supplied
///       at runtime through environment variables, dotnet user-secrets,
///       or <c>appsettings.{Environment}.json</c>.
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Per ADR-028 §决策 G2 the validation deliberately fail-fasts on a
/// structurally-invalid <see cref="ActiveProvider"/> (otherwise the DI
/// composition cannot pick a provider) but does <em>not</em> reject an
/// empty <c>ApiKey</c>: that becomes a per-call
/// <c>LlmErrors.AuthenticationFailed</c> at runtime so the host stays
/// up and the rest of the surface (inbox, runtime status) remains
/// usable while the user configures their key.
/// </remarks>
public sealed class LlmOptions
{
    /// <summary>Configuration section name expected by <c>builder.Configuration.GetSection</c>.</summary>
    public const string SectionName = "Llm";

    /// <summary>Canonical name for the OpenAI provider.</summary>
    public const string OpenAiProviderName = "OpenAI";

    /// <summary>Canonical name for the DeepSeek provider.</summary>
    public const string DeepSeekProviderName = "DeepSeek";

    /// <summary>Canonical name for the Azure OpenAI provider.</summary>
    public const string AzureOpenAiProviderName = "AzureOpenAI";

    /// <summary>
    /// The provider whose <see cref="ILlmProvider"/> implementation gets
    /// registered. Must be one of <see cref="OpenAiProviderName"/>,
    /// <see cref="DeepSeekProviderName"/>, or <see cref="AzureOpenAiProviderName"/>;
    /// case-sensitive.
    /// </summary>
    public string ActiveProvider { get; set; } = OpenAiProviderName;

    /// <summary>Per-provider configuration map keyed by provider name.</summary>
    public LlmProvidersOptions Providers { get; set; } = new();
}

/// <summary>Container for the per-provider sub-sections under <c>Llm:Providers</c>.</summary>
public sealed class LlmProvidersOptions
{
    /// <summary>OpenAI provider settings.</summary>
    public LlmProviderOptions OpenAI { get; set; } = new()
    {
        BaseUrl = "https://api.openai.com/v1",
        Model = "gpt-4o-mini",
    };

    /// <summary>DeepSeek provider settings.</summary>
    public LlmProviderOptions DeepSeek { get; set; } = new()
    {
        BaseUrl = "https://api.deepseek.com",
        Model = "deepseek-chat",
    };

    /// <summary>Azure OpenAI provider settings.</summary>
    public LlmAzureOpenAiProviderOptions AzureOpenAI { get; set; } = new();
}

/// <summary>Settings shared by every provider implementation.</summary>
public sealed class LlmProviderOptions
{
    /// <summary>
    /// API key supplied at runtime. Empty string is allowed and surfaces
    /// at call time as <c>LlmErrors.AuthenticationFailed</c>; this is
    /// the explicit V0 contract per ADR-028 §决策 G2.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>HTTP base URL the named <see cref="System.Net.Http.HttpClient"/> uses; must include scheme.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Default model identifier sent when the request omits <see cref="Application.Abstractions.Llm.LlmRequest.Model"/>.</summary>
    public string Model { get; set; } = string.Empty;
}

/// <summary>
/// Azure OpenAI-specific provider settings. Per ADR-029 Azure OpenAI
/// requires endpoint + deployment ID in addition to API key (rather than
/// the standard BaseUrl + Model). The deployment ID is part of the URL
/// construction and the model is inferred from the deployment.
/// </summary>
public sealed class LlmAzureOpenAiProviderOptions
{
    /// <summary>
    /// Default Azure OpenAI <c>api-version</c> query parameter when the user
    /// leaves <see cref="ApiVersion"/> empty. Tracks a current GA release of
    /// the Chat Completions surface; safe for gpt-4 / gpt-4o / gpt-4.1 family
    /// deployments. Override per-environment when a preview-only feature is
    /// required.
    /// </summary>
    public const string DefaultApiVersion = "2024-10-21";

    /// <summary>
    /// API key supplied at runtime (same as ADR-028 §G2 — empty is allowed,
    /// surfacing as <c>llm.authenticationFailed</c> at call time).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI resource endpoint (e.g., https://my-resource.openai.azure.com/).
    /// Must include scheme and resource name; DeploymentId is appended to form
    /// the full URL path.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI deployment ID (e.g., gpt-4, gpt-4-deployment). Per ADR-029,
    /// this is the model alias managed in the Azure portal, not a full model name.
    /// </summary>
    public string DeploymentId { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI <c>api-version</c> query parameter (e.g., <c>2024-10-21</c>).
    /// Required by the Azure surface on every request — calls without it return
    /// HTTP 404. Empty string falls back to <see cref="DefaultApiVersion"/>.
    /// </summary>
    public string ApiVersion { get; set; } = string.Empty;
}

/// <summary>
/// IOptions validator enforcing the structural constraint of ADR-028
/// §决策 G2 (and extended by ADR-029): <see cref="LlmOptions.ActiveProvider"/>
/// must name a known provider. ApiKey / Endpoint / DeploymentId are
/// intentionally <em>not</em> validated here — see the type-level remarks.
/// </summary>
public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var active = options.ActiveProvider;
        if (string.IsNullOrWhiteSpace(active))
        {
            return ValidateOptionsResult.Fail(
                $"Configuration '{LlmOptions.SectionName}:ActiveProvider' is required."
            );
        }

        if (
            !string.Equals(active, LlmOptions.OpenAiProviderName, StringComparison.Ordinal)
            && !string.Equals(active, LlmOptions.DeepSeekProviderName, StringComparison.Ordinal)
            && !string.Equals(active, LlmOptions.AzureOpenAiProviderName, StringComparison.Ordinal)
        )
        {
            return ValidateOptionsResult.Fail(
                $"Configuration '{LlmOptions.SectionName}:ActiveProvider' must be "
                    + $"'{LlmOptions.OpenAiProviderName}', '{LlmOptions.DeepSeekProviderName}', or "
                    + $"'{LlmOptions.AzureOpenAiProviderName}'; got '{active}'."
            );
        }

        return ValidateOptionsResult.Success;
    }
}
