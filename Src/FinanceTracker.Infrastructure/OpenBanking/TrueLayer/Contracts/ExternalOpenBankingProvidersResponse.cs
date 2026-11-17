namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// TrueLayer's list of banks.
/// </summary>
public class ExternalOpenBankingProvidersResponse
{
    /// <summary>
    /// The banks.
    /// </summary>
    public ExternalOpenBankingProvider[]? Response { get; set; }
}
