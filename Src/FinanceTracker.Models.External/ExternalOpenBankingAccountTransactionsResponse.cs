namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountTransactionsResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccountTransaction> Results { get; set; }
    public string Status { get; set; }
}