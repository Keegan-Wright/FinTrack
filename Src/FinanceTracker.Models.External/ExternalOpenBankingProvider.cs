using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProvider
{
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("logo_url")]
    public string LogoUrl { get; set; } = string.Empty;

    public IAsyncEnumerable<string>? Scopes { get; set; }

}
