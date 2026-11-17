using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// One choice for a sign in step's field.
/// </summary>
public class ExternalOpenBankingProviderFieldValue
{
    /// <summary>
    /// The value sent.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Its name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}
