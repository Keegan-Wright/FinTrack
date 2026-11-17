using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// The bank's own details of a transaction.
/// </summary>
public class ExternalOpenBankingTransactionMetadata
{
    /// <summary>
    /// The bank's reference.
    /// </summary>
    [JsonPropertyName("provider_reference")]
    public string ProviderReference { get; set; } = string.Empty;

    /// <summary>
    /// The bank's own category code, e.g. <c>DD</c>.
    /// </summary>
    [JsonPropertyName("provider_category")]
    public string ProviderCategory { get; set; } = string.Empty;

    /// <summary>
    /// The bank's own transaction type.
    /// </summary>
    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// The bank's id for it.
    /// </summary>
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// The other party's name.
    /// </summary>
    [JsonPropertyName("counter_party_preferred_name")]
    public string CounterPartyPreferredName { get; set; } = string.Empty;

    /// <summary>
    /// The paying account's name.
    /// </summary>
    [JsonPropertyName("debtor_account_name")]
    public string DebtorAccountName { get; set; } = string.Empty;
}
