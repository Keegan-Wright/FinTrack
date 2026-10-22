using FinanceTracker.Contracts.Account;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's bank accounts, for the accounts page and the dashboard snapshot.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Every account the user has connected, each with its latest balance and most recent transactions.
    /// </summary>
    /// <param name="transactionsToReturn">How many of each account's newest transactions to include.</param>
    /// <param name="syncFlags">Not used yet, every account and transaction is returned.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <remarks>
    /// Transaction dates are encrypted, so every transaction is loaded and sorted after decryption.
    /// </remarks>
    IAsyncEnumerable<AccountAndTransactions> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn, SyncTypes syncFlags, CancellationToken cancellationToken);
}
