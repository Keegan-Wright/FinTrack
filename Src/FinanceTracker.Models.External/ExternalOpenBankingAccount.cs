using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccount
{
    [JsonPropertyName("update_timestamp")]
    public DateTime UpdateTimestamp { get; set; }

    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("account_type")]
    public string AccountType { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("account_number")]
    public ExternalOpenBankingAccountNumber? AccountNumber { get; set; }

    public ExternalOpenBankingAccountProvider? Provider { get; set; }
}
