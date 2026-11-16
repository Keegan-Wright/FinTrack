using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Puts what was fetched for an account or card into the database: its balance, standing orders, direct debits and
/// transactions, with the sync log entries that say what was refreshed.
/// </summary>
public interface IAccountSnapshotWriter
{
    /// <summary>
    /// Applies <paramref name="snapshot"/> to <paramref name="account"/>. Transactions are added or updated, and
    /// pending ones the bank no longer reports are removed. Tracked changes, saved by the caller.
    /// </summary>
    Task ApplyAsync(FinanceTrackerContext context, BankAccount account, AccountSnapshot snapshot, DateTime syncedAt,
        ConnectionSyncTally tally, CancellationToken cancellationToken);
}
