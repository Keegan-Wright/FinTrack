using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// One of FinTrack's connections to a bank, from TrueLayer's connection metadata.
/// </summary>
public class ExternalOpenBankingAccountConnection
{
    /// <summary>
    /// FinTrack's client id at TrueLayer.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// TrueLayer's id for the connection.
    /// </summary>
    [JsonPropertyName("credentials_id")]
    public string CredentialsId { get; set; } = string.Empty;

    /// <summary>
    /// Where the consent stands, e.g. <c>Authorised</c>.
    /// </summary>
    [JsonPropertyName("consent_status")]
    public string ConsentStatus { get; set; } = string.Empty;

    /// <summary>
    /// When the consent's status last changed.
    /// </summary>
    [JsonPropertyName("consent_status_updated_at")]
    public DateTime ConsentStatusUpdatedAt { get; set; }

    /// <summary>
    /// When the user consented.
    /// </summary>
    [JsonPropertyName("consent_created_at")]
    public DateTime ConsentCreatedAt { get; set; }

    /// <summary>
    /// When the consent ends.
    /// </summary>
    [JsonPropertyName("consent_expires_at")]
    public DateTime ConsentExpiresAt { get; set; }

    /// <summary>
    /// The bank.
    /// </summary>
    public ExternalOpenBankingAccountConnectionProvider? Provider { get; set; }

    /// <summary>
    /// What the user agreed to share.
    /// </summary>
    public IAsyncEnumerable<string>? Scopes { get; set; }

    /// <summary>
    /// The privacy policy the user agreed to.
    /// </summary>
    [JsonPropertyName("privacy_policy")]
    public string PrivacyPolicy { get; set; } = string.Empty;
}
