using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Account;

namespace FinanceTracker.Infrastructure.Account;

public interface IAccountService
{
    IAsyncEnumerable<AccountAndTransactions> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn, SyncTypes syncFlags, CancellationToken cancellationToken);
}
