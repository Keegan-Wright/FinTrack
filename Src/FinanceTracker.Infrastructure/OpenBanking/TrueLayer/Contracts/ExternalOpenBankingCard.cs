using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A card, as TrueLayer sends it.
/// </summary>
public class ExternalOpenBankingCard
{
    /// <summary>
    /// TrueLayer's id for it.
    /// </summary>
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// The card's network, e.g. <c>VISA</c>.
    /// </summary>
    [JsonPropertyName("card_network")]
    public string? CardNetwork { get; set; }

    /// <summary>
    /// The kind of card, e.g. <c>CREDIT</c>.
    /// </summary>
    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }

    /// <summary>
    /// The currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Its name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The card number's last digits.
    /// </summary>
    [JsonPropertyName("partial_card_number")]
    public string? PartialCardNumber { get; set; }

    /// <summary>
    /// The name on the card.
    /// </summary>
    [JsonPropertyName("name_on_card")]
    public string? NameOnCard { get; set; }

    /// <summary>
    /// When the card expires.
    /// </summary>
    [JsonPropertyName("valid_to")]
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// When the bank last updated it.
    /// </summary>
    [JsonPropertyName("update_timestamp")]
    public DateTime UpdateTimestamp { get; set; }

    /// <summary>
    /// The bank.
    /// </summary>
    public ExternalOpenBankingAccountProvider? Provider { get; set; }
}
