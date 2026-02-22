namespace FinanceTracker.Models.External;

public class ExternalOpenBankingProviderStep
{
    public string Title { get; set; }
    public IAsyncEnumerable<ExternalOpenBankingProviderStepField> Fields { get; set; }
}
