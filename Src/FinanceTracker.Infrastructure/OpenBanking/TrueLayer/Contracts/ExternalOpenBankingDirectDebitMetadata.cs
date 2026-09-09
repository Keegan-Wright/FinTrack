using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingDirectDebitMetadata
{
    [JsonPropertyName("provider_mandate_identification")]
    public string ProviderMandateIdentification { get; set; } = string.Empty;

    [JsonPropertyName("provider_account_id")]
    public string ProviderAccountId { get; set; } = string.Empty;
}
