using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// Which debt a transaction pays off, and who said so.
/// </summary>
public class TransactionDebt
{
    /// <summary>
    /// The debt it pays off, null when the user said it pays none.
    /// </summary>
    [Description("The debt the transaction pays off, null when the user said it pays none")]
    public Guid? DebtId { get; init; }

    /// <summary>
    /// The debt's name, null when it pays none.
    /// </summary>
    [Description("Name of the debt the transaction pays off")]
    public string? DebtName { get; init; }

    /// <summary>
    /// Who decided, a rule or the user.
    /// </summary>
    [Description("Who decided which debt the transaction pays off")]
    public TransferDecidedBy DecidedBy { get; init; }

    /// <summary>
    /// The rule that decided, null unless <see cref="DecidedBy"/> is a rule.
    /// </summary>
    [Description("Name of the rule that decided, for a rule's decision")]
    public string? RuleName { get; init; }
}
