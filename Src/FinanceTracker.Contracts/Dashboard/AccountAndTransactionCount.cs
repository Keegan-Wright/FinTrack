using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

/// <summary>
/// Not used, the account service takes these as parameters.
/// </summary>
public class AccountAndTransactionCount
{
    /// <summary>
    /// How many of each account's newest transactions to include.
    /// </summary>
    [Description("Number of transactions to retrieve")]
    public required int TransactionsCount { get; init; }

    /// <summary>
    /// Which kinds of synced data to include.
    /// </summary>
    [Description("Type of synchronization to perform")]
    public required SyncTypes SyncTypes { get; init; }
}
