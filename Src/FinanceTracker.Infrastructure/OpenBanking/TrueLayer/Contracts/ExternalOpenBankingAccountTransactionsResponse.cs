namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's list of transactions, settled or pending.
/// </summary>
public class ExternalOpenBankingAccountTransactionsResponse
{
    /// <summary>
    /// What was found.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingAccountTransaction>? Results { get; set; }

    /// <summary>
    /// Whether TrueLayer found it, e.g. <c>Succeeded</c>.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
