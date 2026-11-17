using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A bank TrueLayer can connect to.
/// </summary>
public class ExternalOpenBankingProvider
{
    /// <summary>
    /// TrueLayer's id for the bank.
    /// </summary>
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Its name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The bank's country code, e.g. <c>uk</c>.
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Where the bank's logo is.
    /// </summary>
    [JsonPropertyName("logo_url")]
    public string LogoUrl { get; set; } = string.Empty;

    /// <summary>
    /// The scopes the bank supports.
    /// </summary>
    public IAsyncEnumerable<string>? Scopes { get; set; }
}
