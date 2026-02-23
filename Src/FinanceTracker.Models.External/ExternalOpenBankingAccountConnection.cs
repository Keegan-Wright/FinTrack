using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountConnection
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("credentials_id")]
    public string CredentialsId { get; set; } = string.Empty;

    [JsonPropertyName("consent_status")]
    public string ConsentStatus { get; set; } = string.Empty;

    [JsonPropertyName("consent_status_updated_at")]
    public DateTime ConsentStatusUpdatedAt { get; set; }

    [JsonPropertyName("consent_created_at")]
    public DateTime ConsentCreatedAt { get; set; }

    [JsonPropertyName("consent_expires_at")]
    public DateTime ConsentExpiresAt { get; set; }

    public ExternalOpenBankingAccountConnectionProvider? Provider { get; set; }
    public IAsyncEnumerable<string>? Scopes { get; set; }

    [JsonPropertyName("privacy_policy")]
    public string PrivacyPolicy { get; set; } = string.Empty;
}
