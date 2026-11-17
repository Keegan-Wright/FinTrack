namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's list of an account's standing orders.
/// </summary>
public class ExternalOpenBankingAccountStandingOrdersResponse
{
    /// <summary>
    /// What was found.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingAccountStandingOrder>? Results { get; set; }

    /// <summary>
    /// Whether TrueLayer found it, e.g. <c>Succeeded</c>.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
