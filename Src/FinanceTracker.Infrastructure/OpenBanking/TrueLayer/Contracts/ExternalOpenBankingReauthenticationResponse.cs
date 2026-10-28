using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingReauthenticationResponse
{
    /// <summary>
    /// Where to send the user to sign in to their bank again.
    /// </summary>
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
