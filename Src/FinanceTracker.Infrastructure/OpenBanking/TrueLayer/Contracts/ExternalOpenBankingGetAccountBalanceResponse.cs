namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingGetAccountBalanceResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccountBalance>? Results { get; set; }
    public string Status { get; set; } = string.Empty;
}
