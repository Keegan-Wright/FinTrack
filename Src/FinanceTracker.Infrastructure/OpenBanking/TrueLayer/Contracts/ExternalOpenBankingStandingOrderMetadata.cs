using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingStandingOrderMetadata
{
    [JsonPropertyName("provider_account_id")]
    public string ProviderAccountId { get; set; } = string.Empty;
}
