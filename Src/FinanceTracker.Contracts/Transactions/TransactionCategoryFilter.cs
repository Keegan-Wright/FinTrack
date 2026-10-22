using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// A provider category the user can filter transactions by.
/// </summary>
public class TransactionCategoryFilter
{
    /// <summary>
    /// The category, for <see cref="FilterTransactions.Categories"/>.
    /// </summary>
    [Description("Category name for filtering transactions")]
    public required string TransactionCategory { get; init; }
}
