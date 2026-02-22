namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountDirectDebitsResponse
{
    public IAsyncEnumerable<ExternalOpenBankingDirectDebit>? Results { get; set; }
    public string Status { get; set; }
}
