namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingAccountConnectionResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccountConnection>? Results { get; set; }
}
