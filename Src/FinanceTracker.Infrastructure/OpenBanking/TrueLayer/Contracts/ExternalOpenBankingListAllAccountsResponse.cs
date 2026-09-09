namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingListAllAccountsResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccount>? Results { get; set; }
    public string Status { get; set; } = string.Empty;
}
