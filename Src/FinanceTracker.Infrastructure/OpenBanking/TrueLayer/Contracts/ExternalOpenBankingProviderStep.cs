namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

/// <summary>
/// A step a bank's sign in asks for.
/// </summary>
public class ExternalOpenBankingProviderStep
{
    /// <summary>
    /// The step's title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The step's fields.
    /// </summary>
    public IAsyncEnumerable<ExternalOpenBankingProviderStepField>? Fields { get; set; }
}
