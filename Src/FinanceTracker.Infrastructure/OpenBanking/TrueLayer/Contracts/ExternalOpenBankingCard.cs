using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingCard
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("card_network")]
    public string? CardNetwork { get; set; }

    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }

    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("partial_card_number")]
    public string? PartialCardNumber { get; set; }

    [JsonPropertyName("name_on_card")]
    public string? NameOnCard { get; set; }

    [JsonPropertyName("valid_to")]
    public DateTime? ValidTo { get; set; }

    [JsonPropertyName("update_timestamp")]
    public DateTime UpdateTimestamp { get; set; }

    public ExternalOpenBankingAccountProvider? Provider { get; set; }
}
