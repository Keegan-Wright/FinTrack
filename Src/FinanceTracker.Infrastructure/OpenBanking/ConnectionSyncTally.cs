namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// What one connection's sync did, for its summary log and the tags on its span.
/// </summary>
public sealed class ConnectionSyncTally
{
    private int _failedFetches;

    /// <summary>
    /// The accounts synced.
    /// </summary>
    public int Accounts { get; set; }

    /// <summary>
    /// The cards synced.
    /// </summary>
    public int Cards { get; set; }

    /// <summary>
    /// Transactions added for the first time.
    /// </summary>
    public int TransactionsAdded { get; set; }

    /// <summary>
    /// Transactions that changed.
    /// </summary>
    public int TransactionsUpdated { get; set; }

    /// <summary>
    /// Pending transactions removed, settled or dropped by the bank.
    /// </summary>
    public int PendingRemoved { get; set; }

    /// <summary>
    /// Fetches from the bank that failed, each logged and the rest of the sync carrying on without that data.
    /// </summary>
    public int FailedFetches => _failedFetches;

    /// <summary>
    /// Counts a failed fetch. An account's fetches run at the same time, so it's thread safe.
    /// </summary>
    public void RecordFetchFailed() => Interlocked.Increment(ref _failedFetches);
}
