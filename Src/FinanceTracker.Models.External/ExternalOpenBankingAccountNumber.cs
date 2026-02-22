using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountNumber
{
    public string Iban { get; set; }

    [JsonPropertyName("swift_bic")]
    public string SwiftBic { get; set; }

    public string Number { get; set; }

    [JsonPropertyName("sort_code")]
    public string SortCode { get; set; }
}
