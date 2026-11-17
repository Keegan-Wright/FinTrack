using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A field in a bank's sign in step.
/// </summary>
public class ExternalOpenBankingProviderStepField
{
    /// <summary>
    /// The kind of field.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The choices, for a field that offers them.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingProviderFieldValue>? Values { get; set; }

    /// <summary>
    /// The field's id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Its name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Help shown with the field.
    /// </summary>
    [JsonPropertyName("help_text")]
    public string HelpText { get; set; } = string.Empty;

    /// <summary>
    /// Whether it must be filled in.
    /// </summary>
    public bool Mandatory { get; set; }

    /// <summary>
    /// Whether it's a secret, such as a password.
    /// </summary>
    [JsonPropertyName("is_sensitive")]
    public bool IsSensitive { get; set; }

    /// <summary>
    /// The checks on its value.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingProviderStepValidation>? Validations { get; set; }

    /// <summary>
    /// The characters it takes.
    /// </summary>
    [JsonPropertyName("allowed_characters")]
    public string AllowedCharacters { get; set; } = string.Empty;
}
