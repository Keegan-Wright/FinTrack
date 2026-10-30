using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// A tag one of the user's rules added to a transaction.
/// </summary>
public class TransactionRuleTag
{
    /// <summary>
    /// The tag's name.
    /// </summary>
    [Description("The tag's name")]
    public required string Tag { get; init; }

    /// <summary>
    /// The rule that added it.
    /// </summary>
    [Description("The rule that added it")]
    public Guid RuleId { get; init; }

    /// <summary>
    /// The rule's name.
    /// </summary>
    [Description("The name of the rule that added it")]
    public required string RuleName { get; init; }
}
