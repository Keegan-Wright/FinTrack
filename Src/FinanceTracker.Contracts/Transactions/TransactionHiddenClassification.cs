using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// One of the bank's classifications hidden on a transaction, so it's left out of its tags, filters and reports.
/// </summary>
public class TransactionHiddenClassification
{
    /// <summary>
    /// As the bank sent it.
    /// </summary>
    [Description("The bank's classification, as the bank sent it")]
    public required string Classification { get; init; }

    /// <summary>
    /// The rule that hid it, null when the user did. Only the user's can be shown again from the transaction.
    /// </summary>
    [Description("The name of the rule that hid it, null when the user did")]
    public string? RuleName { get; init; }
}
