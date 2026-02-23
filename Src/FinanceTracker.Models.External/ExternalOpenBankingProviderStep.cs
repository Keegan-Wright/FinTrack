namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProviderStep
{
    public string Title { get; set; } = string.Empty;
    public IAsyncEnumerable<ExternalOpenBankingProviderStepField>? Fields { get; set; }
}
