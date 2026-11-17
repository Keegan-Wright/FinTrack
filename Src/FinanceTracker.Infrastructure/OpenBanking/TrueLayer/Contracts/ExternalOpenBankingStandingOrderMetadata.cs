using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// The bank's own details of a standing order.
/// </summary>
public class ExternalOpenBankingStandingOrderMetadata
{
    /// <summary>
    /// The bank's id for the account.
    /// </summary>
    [JsonPropertyName("provider_account_id")]
    public string ProviderAccountId { get; set; } = string.Empty;
}
