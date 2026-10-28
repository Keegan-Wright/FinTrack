using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingReauthenticationRequest
{
    [JsonPropertyName("response_type")]
    public string ResponseType { get; set; } = "code";

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }

    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; set; }
}
