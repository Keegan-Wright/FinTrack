using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProvider
{
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    public string Country { get; set; }

    [JsonPropertyName("logo_url")]
    public string LogoUrl { get; set; }

    public IAsyncEnumerable<string> Scopes { get; set; }

    [JsonPropertyName("provider_scope_mappings")]
    public ExternalOpenBankingProviderScopeMappings ProviderScopeMappings { get; set; }

    public ExternalOpenBankingProviderStep[] steps { get; set; }
}
