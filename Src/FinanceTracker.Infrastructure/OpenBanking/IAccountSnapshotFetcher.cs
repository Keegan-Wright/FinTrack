using FinanceTracker.Contracts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Asks the bank for what a sync wants of one account or card, the fetches running at the same time, each in its
/// own span. A failed fetch is logged and counted, and the sync carries on without that data.
/// </summary>
public interface IAccountSnapshotFetcher
{
    /// <summary>
    /// The account's balance, standing orders, direct debits and transactions, as far as
    /// <paramref name="syncFlags"/> asks.
    /// </summary>
    /// <param name="enhancedAuthenticationWindowFlags">What to fetch of standing orders and direct debits, which
    /// banks only release shortly after the user authenticated.</param>
    /// <param name="transactionsFrom">Where booked transactions are fetched from, null for all of them.</param>
    Task<AccountSnapshot> FetchAccountAsync(BankConnection connection, BankAccount account, string authToken,
        SyncTypes syncFlags, SyncTypes enhancedAuthenticationWindowFlags, DateTime? transactionsFrom,
        ConnectionSyncTally tally, CancellationToken cancellationToken);

    /// <summary>
    /// The card's balance and transactions, as far as <paramref name="syncFlags"/> asks. Cards have no standing
    /// orders or direct debits.
    /// </summary>
    /// <param name="transactionsFrom">Where booked transactions are fetched from, null for all of them.</param>
    Task<AccountSnapshot> FetchCardAsync(BankConnection connection, BankAccount card, string authToken,
        SyncTypes syncFlags, DateTime? transactionsFrom, ConnectionSyncTally tally,
        CancellationToken cancellationToken);
}
