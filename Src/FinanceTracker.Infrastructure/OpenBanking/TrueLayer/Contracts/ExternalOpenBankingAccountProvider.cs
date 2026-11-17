using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// The bank an account or card is with.
/// </summary>
public class ExternalOpenBankingAccountProvider
{
    /// <summary>
    /// Its name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// TrueLayer's id for the bank.
    /// </summary>
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Where the bank's logo is.
    /// </summary>
    [JsonPropertyName("logo_uri")]
    public string LogoUri { get; set; } = string.Empty;
}
