using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountNumber
{
    public string Iban { get; set; } = string.Empty;

    [JsonPropertyName("swift_bic")]
    public string SwiftBic { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("sort_code")]
    public string SortCode { get; set; } = string.Empty;
}
