using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

public class AccountAndTransactionCount
{
    [Description("Number of transactions to retrieve")]
    public required int TransactionsCount { get; init; }

    [Description("Type of synchronization to perform")]
    public required SyncTypes SyncTypes { get; init; }
}
