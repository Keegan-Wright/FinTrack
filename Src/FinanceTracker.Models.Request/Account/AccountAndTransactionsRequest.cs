using System.ComponentModel;
using FinanceTracker.Enums;

namespace FinanceTracker.Models.Request;

public class AccountAndTransactionsRequest
{
    [Description("Number of transactions to retrieve")]
    public int TransactionsCount { get; set; }

    [Description("Type of synchronization to perform")]
    public SyncTypes SyncTypes { get; set; }
}