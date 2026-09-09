using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Account;

namespace FinanceTracker.Infrastructure.Account;

public interface IAccountService
{
    IAsyncEnumerable<AccountAndTransactionsResponse> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn, SyncTypes syncFlags, CancellationToken cancellationToken);
}
