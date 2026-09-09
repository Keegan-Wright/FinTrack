using System.ComponentModel;

namespace FinanceTracker.Contracts.Transaction;

public class TransactionTypeFilterResponse
{
    [Description("Type name for filtering transactions")]
    public required string TransactionType { get; init; }
}
