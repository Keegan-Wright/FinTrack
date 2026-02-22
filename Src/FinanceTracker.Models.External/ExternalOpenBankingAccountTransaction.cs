using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountTransaction
{
    public DateTime Timestamp { get; set; }
    public string Description { get; set; }

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; }

    [JsonPropertyName("transaction_category")]
    public string TransactionCategory { get; set; }

    [JsonPropertyName("transaction_classification")]
    public IAsyncEnumerable<string> TransactionClassification { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; }

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; }

    [JsonPropertyName("provider_transaction_id")]
    public string ProviderTransactionId { get; set; }

    [JsonPropertyName("normalised_provider_transaction_id")]
    public string NormalisedProviderTransactionId { get; set; }

    public ExternalOpenBankingTransactionMetadata Meta { get; set; }

    [JsonPropertyName("merchant_name")]
    public string MerchantName { get; set; }
}
