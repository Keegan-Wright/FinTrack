using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A transaction, as TrueLayer sends it.
/// </summary>
public class ExternalOpenBankingAccountTransaction
{
    /// <summary>
    /// When it happened.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The bank's description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// <c>DEBIT</c> or <c>CREDIT</c>.
    /// </summary>
    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// TrueLayer's category, e.g. <c>PURCHASE</c>.
    /// </summary>
    [JsonPropertyName("transaction_category")]
    public string TransactionCategory { get; set; } = string.Empty;

    /// <summary>
    /// TrueLayer's classifications, e.g. <c>Shopping</c>.
    /// </summary>
    [JsonPropertyName("transaction_classification")]
    public IAsyncEnumerable<string>? TransactionClassification { get; set; }

    /// <summary>
    /// How much, negative for money out.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// TrueLayer's id for it.
    /// </summary>
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// The bank's id for it.
    /// </summary>
    [JsonPropertyName("provider_transaction_id")]
    public string ProviderTransactionId { get; set; } = string.Empty;

    /// <summary>
    /// The bank's id for it, kept the same once it settles.
    /// </summary>
    [JsonPropertyName("normalised_provider_transaction_id")]
    public string NormalisedProviderTransactionId { get; set; } = string.Empty;

    /// <summary>
    /// The bank's own details.
    /// </summary>
    public ExternalOpenBankingTransactionMetadata? Meta { get; set; }

    /// <summary>
    /// The merchant on a card payment.
    /// </summary>
    [JsonPropertyName("merchant_name")]
    public string MerchantName { get; set; } = string.Empty;
}
