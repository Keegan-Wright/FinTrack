using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's answer to asking to renew a connection's consent.
/// </summary>
public class ExternalOpenBankingReauthenticationResponse
{
    /// <summary>
    /// Where to send the user to sign in to their bank again.
    /// </summary>
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// Whether TrueLayer can renew it.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
