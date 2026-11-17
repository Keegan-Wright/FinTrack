using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// An account's numbers, as TrueLayer sends them.
/// </summary>
public class ExternalOpenBankingAccountNumber
{
    /// <summary>
    /// The IBAN.
    /// </summary>
    public string Iban { get; set; } = string.Empty;

    /// <summary>
    /// The bank's SWIFT code.
    /// </summary>
    [JsonPropertyName("swift_bic")]
    public string SwiftBic { get; set; } = string.Empty;

    /// <summary>
    /// The account number.
    /// </summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// The sort code.
    /// </summary>
    [JsonPropertyName("sort_code")]
    public string SortCode { get; set; } = string.Empty;
}
