namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's answer with an account's or card's balance.
/// </summary>
public class ExternalOpenBankingGetAccountBalanceResponse
{
    /// <summary>
    /// What was found.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingAccountBalance>? Results { get; set; }

    /// <summary>
    /// Whether TrueLayer found it, e.g. <c>Succeeded</c>.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
