namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountConnectionResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccountConnection> Results { get; set; }
}