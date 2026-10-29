namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

public class ExternalOpenBankingListAllCardsResponse
{
    public IAsyncEnumerable<ExternalOpenBankingCard>? Results { get; set; }
    public string Status { get; set; } = string.Empty;
}
