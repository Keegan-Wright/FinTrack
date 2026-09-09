using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

public class TransactionTypeFilter
{
    [Description("Type name for filtering transactions")]
    public required string TransactionType { get; init; }
}
