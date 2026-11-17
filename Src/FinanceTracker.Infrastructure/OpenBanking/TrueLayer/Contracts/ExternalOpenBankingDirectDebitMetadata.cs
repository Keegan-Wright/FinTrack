using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// The bank's own details of a direct debit.
/// </summary>
public class ExternalOpenBankingDirectDebitMetadata
{
    /// <summary>
    /// The bank's id for the mandate.
    /// </summary>
    [JsonPropertyName("provider_mandate_identification")]
    public string ProviderMandateIdentification { get; set; } = string.Empty;

    /// <summary>
    /// The bank's id for the account.
    /// </summary>
    [JsonPropertyName("provider_account_id")]
    public string ProviderAccountId { get; set; } = string.Empty;
}
