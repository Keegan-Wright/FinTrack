using System.ComponentModel;
using FinanceTracker.Enums;

namespace FinanceTracker.Models.Request.Dashboard;

public class AccountAndTransactionsRequest
{
    [Description("Number of transactions to retrieve")]
    public required int TransactionsCount { get; init; }

    [Description("Type of synchronization to perform")]
    public required SyncTypes SyncTypes { get; init; }
}
