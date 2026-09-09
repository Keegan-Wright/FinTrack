namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingAccountStandingOrdersResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccountStandingOrder>? Results { get; set; }
    public string Status { get; set; } = string.Empty;
}
