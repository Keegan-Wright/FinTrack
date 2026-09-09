using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

public class TransactionCategoryFilter
{
    [Description("Category name for filtering transactions")]
    public required string TransactionCategory { get; init; }
}
