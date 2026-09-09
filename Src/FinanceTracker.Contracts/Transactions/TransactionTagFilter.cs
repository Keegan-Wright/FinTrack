using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

public class TransactionTagFilter
{
    [Description("Tag name for filtering transactions")]
    public required string Tag { get; init; }
}
