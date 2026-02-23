using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingStandingOrderMetadata
{
    [JsonPropertyName("provider_account_id")]
    public string ProviderAccountId { get; set; } = string.Empty;
}
