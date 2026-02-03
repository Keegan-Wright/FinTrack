using FinanceTracker.Enums;
using FinanceTracker.Models.Response.Account;

namespace FinanceTracker.Services.Account;

public interface IAccountService
{
    IAsyncEnumerable<AccountAndTransactionsResponse> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn, SyncTypes syncFlags, CancellationToken cancellationToken);
}