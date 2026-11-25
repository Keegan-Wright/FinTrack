namespace FinanceTracker.Ai.Tools.Spending;

/// <summary>
/// What a spending breakdown is grouped by.
/// </summary>
public enum SpendingGrouping
{
    /// <summary>
    /// By tag, such as Groceries. A transaction with several tags counts towards each.
    /// </summary>
    Tag,

    /// <summary>
    /// By account.
    /// </summary>
    Account
}
