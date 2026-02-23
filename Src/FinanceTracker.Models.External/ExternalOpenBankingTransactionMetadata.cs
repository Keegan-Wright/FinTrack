using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingTransactionMetadata
{
    [JsonPropertyName("provider_category")]
    public string ProviderCategory { get; set; } = string.Empty;

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = string.Empty;

    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("counter_party_preferred_name")]
    public string CounterPartyPreferredName { get; set; } = string.Empty;

    [JsonPropertyName("debtor_account_name")]
    public string DebtorAccountName { get; set; } = string.Empty;
}
