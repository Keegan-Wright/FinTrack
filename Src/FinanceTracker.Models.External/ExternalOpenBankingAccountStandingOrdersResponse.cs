namespace FinanceTracker.Models.External;

public class ExternalOpenBankingAccountStandingOrdersResponse
{
    public IAsyncEnumerable<ExternalOpenBankingAccountStandingOrder>? Results { get; set; }
    public string Status { get; set; } = string.Empty;
}
