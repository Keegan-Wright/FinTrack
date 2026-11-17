using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// What's sent to TrueLayer to renew a connection's consent.
/// </summary>
public class ExternalOpenBankingReauthenticationRequest
{
    /// <summary>
    /// What TrueLayer gives back, <c>code</c>.
    /// </summary>
    [JsonPropertyName("response_type")]
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// The connection's latest refresh token.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }

    /// <summary>
    /// Where the bank sends the user back to.
    /// </summary>
    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; set; }
}
