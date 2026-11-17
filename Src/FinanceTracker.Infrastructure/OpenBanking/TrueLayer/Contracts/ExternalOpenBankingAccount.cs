using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// An account, as TrueLayer sends it.
/// </summary>
public class ExternalOpenBankingAccount
{
    /// <summary>
    /// When the bank last updated it.
    /// </summary>
    [JsonPropertyName("update_timestamp")]
    public DateTime UpdateTimestamp { get; set; }

    /// <summary>
    /// TrueLayer's id for it.
    /// </summary>
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// What it is, e.g. <c>TRANSACTION</c> or <c>SAVINGS</c>.
    /// </summary>
    [JsonPropertyName("account_type")]
    public string AccountType { get; set; } = string.Empty;

    /// <summary>
    /// Its name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Its numbers.
    /// </summary>
    [JsonPropertyName("account_number")]
    public ExternalOpenBankingAccountNumber? AccountNumber { get; set; }

    /// <summary>
    /// The bank.
    /// </summary>
    public ExternalOpenBankingAccountProvider? Provider { get; set; }
}
