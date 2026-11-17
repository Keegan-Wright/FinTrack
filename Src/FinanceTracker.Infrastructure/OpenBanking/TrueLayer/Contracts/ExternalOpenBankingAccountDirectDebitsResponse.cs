namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's list of an account's direct debits.
/// </summary>
public class ExternalOpenBankingAccountDirectDebitsResponse
{
    /// <summary>
    /// What was found.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingDirectDebit>? Results { get; set; }

    /// <summary>
    /// Whether TrueLayer found it, e.g. <c>Succeeded</c>.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
