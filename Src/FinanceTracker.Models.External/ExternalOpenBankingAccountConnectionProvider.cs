using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountConnectionProvider
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("logo_uri")]
    public string LogoUri { get; set; }

    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; }
}
