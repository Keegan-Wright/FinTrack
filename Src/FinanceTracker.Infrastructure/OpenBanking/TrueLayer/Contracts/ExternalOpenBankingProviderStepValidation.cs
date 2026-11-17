using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A check on a sign in field's value.
/// </summary>
public class ExternalOpenBankingProviderStepValidation
{
    /// <summary>
    /// The pattern the value has to match.
    /// </summary>
    public string Regex { get; set; } = string.Empty;

    /// <summary>
    /// What's said when it doesn't.
    /// </summary>
    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;
}
