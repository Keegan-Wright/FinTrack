namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's connection metadata.
/// </summary>
public class ExternalOpenBankingAccountConnectionResponse
{
    /// <summary>
    /// What was found.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingAccountConnection>? Results { get; set; }
}
