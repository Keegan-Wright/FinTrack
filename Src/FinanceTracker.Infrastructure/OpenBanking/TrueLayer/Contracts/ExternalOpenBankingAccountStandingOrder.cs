using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A standing order, as TrueLayer sends it.
/// </summary>
public class ExternalOpenBankingAccountStandingOrder
{
    /// <summary>
    /// How often it pays, as the bank describes it.
    /// </summary>
    public string Frequency { get; set; } = string.Empty;

    /// <summary>
    /// The bank's status for it, e.g. active.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// When TrueLayer read it.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The bank's own details.
    /// </summary>
    public ExternalOpenBankingStandingOrderMetadata? Meta { get; set; }

    /// <summary>
    /// When it next pays.
    /// </summary>
    [JsonPropertyName("next_payment_date")]
    public DateTime NextPaymentDate { get; set; }

    /// <summary>
    /// What it next pays.
    /// </summary>
    [JsonPropertyName("next_payment_amount")]
    public decimal NextPaymentAmount { get; set; }

    /// <summary>
    /// When it first paid.
    /// </summary>
    [JsonPropertyName("first_payment_date")]
    public DateTime FirstPaymentDate { get; set; }

    /// <summary>
    /// What it first paid.
    /// </summary>
    [JsonPropertyName("first_payment_amount")]
    public decimal FirstPaymentAmount { get; set; }

    /// <summary>
    /// When it last pays.
    /// </summary>
    [JsonPropertyName("final_payment_date")]
    public DateTime FinalPaymentDate { get; set; }

    /// <summary>
    /// What it last pays.
    /// </summary>
    [JsonPropertyName("final_payment_amount")]
    public decimal FinalPaymentAmount { get; set; }

    /// <summary>
    /// The reference on its payments.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Who it pays.
    /// </summary>
    public string Payee { get; set; } = string.Empty;
}
