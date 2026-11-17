using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A direct debit, as TrueLayer sends it.
/// </summary>
public class ExternalOpenBankingDirectDebit
{
    /// <summary>
    /// TrueLayer's id for it.
    /// </summary>
    [JsonPropertyName("direct_debit_id")]
    public string DirectDebitId { get; set; } = string.Empty;

    /// <summary>
    /// When TrueLayer read it.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Who it pays.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The bank's status for it, e.g. active.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// When it last paid.
    /// </summary>
    [JsonPropertyName("previous_payment_timestamp")]
    public DateTime PreviousPaymentTimestamp { get; set; }

    /// <summary>
    /// What it last paid.
    /// </summary>
    [JsonPropertyName("previous_payment_amount")]
    public decimal PreviousPaymentAmount { get; set; }

    /// <summary>
    /// The currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The bank's own details.
    /// </summary>
    public ExternalOpenBankingDirectDebitMetadata? Meta { get; set; }
}
