using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingDirectDebit
{
    [JsonPropertyName("direct_debit_id")]
    public string DirectDebitId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("previous_payment_timestamp")]
    public DateTime PreviousPaymentTimestamp { get; set; }

    [JsonPropertyName("previous_payment_amount")]
    public decimal PreviousPaymentAmount { get; set; }

    public string Currency { get; set; } = string.Empty;
    public ExternalOpenBankingDirectDebitMetadata? Meta { get; set; }
}
