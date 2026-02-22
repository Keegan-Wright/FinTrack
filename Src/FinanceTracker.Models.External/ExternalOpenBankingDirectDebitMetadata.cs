using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingDirectDebitMetadata
{
    [JsonPropertyName("provider_mandate_identification")]
    public string ProviderMandateIdentification { get; set; }

    [JsonPropertyName("provider_account_id")]
    public string ProviderAccountId { get; set; }
}
