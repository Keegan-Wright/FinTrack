using System.ComponentModel;

namespace FinanceTracker.Contracts.Transaction;

public class TransactionCategoryFilterResponse
{
    [Description("Category name for filtering transactions")]
    public required string TransactionCategory { get; init; }
}
