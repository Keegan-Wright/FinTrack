using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountBalance
{
    public string Currency { get; set; } = string.Empty;
    public decimal Available { get; set; }
    public decimal Current { get; set; }

    [JsonPropertyName("update_timestamp ")]
    public DateTime UpdateTimestamp { get; set; }
}
