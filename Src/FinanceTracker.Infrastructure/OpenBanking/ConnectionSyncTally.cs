namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// What one connection's sync did, for its summary log and the tags on its span.
/// </summary>
internal sealed class ConnectionSyncTally
{
    private int _failedFetches;

    public int Accounts { get; set; }

    public int Cards { get; set; }

    public int TransactionsAdded { get; set; }

    public int TransactionsUpdated { get; set; }

    public int PendingRemoved { get; set; }

    /// <summary>
    /// Fetches from the bank that failed, each logged and the rest of the sync carrying on without that data.
    /// </summary>
    public int FailedFetches => _failedFetches;

    // An account's fetches run at the same time
    public void RecordFetchFailed() => Interlocked.Increment(ref _failedFetches);
}
