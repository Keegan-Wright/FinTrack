using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// An account's or card's balance, as TrueLayer sends it.
/// </summary>
public class ExternalOpenBankingAccountBalance
{
    /// <summary>
    /// The currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// What can be spent now, or for a card the credit left.
    /// </summary>
    public decimal Available { get; set; }

    /// <summary>
    /// The balance, or for a card what's owed.
    /// </summary>
    public decimal Current { get; set; }

    /// <summary>
    /// When the bank last updated it.
    /// </summary>
    [JsonPropertyName("update_timestamp")]
    public DateTime UpdateTimestamp { get; set; }

    /// <summary>
    /// A card's credit limit, cards only.
    /// </summary>
    [JsonPropertyName("credit_limit")]
    public decimal? CreditLimit { get; set; }
}
