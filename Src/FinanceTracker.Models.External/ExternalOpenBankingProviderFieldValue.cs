using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProviderFieldValue
{
    public string Value { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }
}