using System.Text.Json.Serialization;

namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountStandingOrder
{
    public string Frequency { get; set; }
    public string Status { get; set; }
    public DateTime Timestamp { get; set; }
    public string Currency { get; set; }
    public ExternalOpenBankingStandingOrderMetadata Meta { get; set; }

    [JsonPropertyName("next_payment_date")]
    public DateTime NextPaymentDate { get; set; }

    [JsonPropertyName("next_payment_amount")]
    public int NextPaymentAmount { get; set; }

    [JsonPropertyName("first_payment_date")]
    public DateTime FirstPaymentDate { get; set; }

    [JsonPropertyName("first_payment_amount")]
    public int FirstPaymentAmount { get; set; }

    [JsonPropertyName("final_payment_date")]
    public DateTime FinalPaymentDate { get; set; }

    [JsonPropertyName("final_payment_amount")]
    public int FinalPaymentAmount { get; set; }
    public string Reference { get; set; }
    public string Payee { get; set; }
}