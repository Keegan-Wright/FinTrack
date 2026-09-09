namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingAccountTransactionsResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccountTransaction>? Results { get; set; }
    public string Status { get; set; } = string.Empty;
}
