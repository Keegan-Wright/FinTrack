namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's list of a connection's cards.
/// </summary>
public class ExternalOpenBankingListAllCardsResponse
{
    /// <summary>
    /// What was found.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingCard>? Results { get; set; }

    /// <summary>
    /// Whether TrueLayer found it, e.g. <c>Succeeded</c>.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
