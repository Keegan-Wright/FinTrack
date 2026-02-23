using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProviderStepField
{
    public string Type { get; set; } = string.Empty;
    public IAsyncEnumerable<ExternalOpenBankingProviderFieldValue>? Values { get; set; }
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("help_text")]
    public string HelpText { get; set; } = string.Empty;

    public bool Mandatory { get; set; }

    [JsonPropertyName("is_sensitive")]
    public bool IsSensitive { get; set; }

    public IAsyncEnumerable<ExternalOpenBankingProviderStepValidation>? Validations { get; set; }

    [JsonPropertyName("allowed_characters")]
    public string AllowedCharacters { get; set; } = string.Empty;
}
