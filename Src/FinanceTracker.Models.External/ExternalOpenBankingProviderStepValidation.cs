using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProviderStepValidation
{
    public string Regex { get; set; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;
}
