using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingProviderFieldValue
{
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}
