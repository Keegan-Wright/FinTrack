using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// Which bill a transaction pays, and who said so.
/// </summary>
public class TransactionBill
{
    /// <summary>
    /// The bill it pays, null when the user said it pays none.
    /// </summary>
    [Description("The bill the transaction pays, null when the user said it pays none")]
    public Guid? BillId { get; init; }

    /// <summary>
    /// The bill's name, null when it pays none.
    /// </summary>
    [Description("Name of the bill the transaction pays")]
    public string? BillName { get; init; }

    /// <summary>
    /// Whether the bill is a subscription FinTrack spotted from repeat payments, rather than a standing order, direct
    /// debit or one the user added.
    /// </summary>
    [Description("Whether the bill is a subscription spotted from repeat payments")]
    public bool IsSubscription { get; init; }

    /// <summary>
    /// Who decided, FinTrack's matching, a rule or the user.
    /// </summary>
    [Description("Who decided which bill the transaction pays")]
    public TransferDecidedBy DecidedBy { get; init; }

    /// <summary>
    /// The rule that decided, null unless <see cref="DecidedBy"/> is a rule.
    /// </summary>
    [Description("Name of the rule that decided, for a rule's decision")]
    public string? RuleName { get; init; }
}
