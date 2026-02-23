namespace FinanceTracker.Models.External;

public class ExternalOpenBankingListAllAccountsResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccount>? Results { get; set; }
    public string Status { get; set; } = string.Empty;
}
