using System.ComponentModel;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Contracts.Debts;

/// <summary>
/// A transaction that pays off a debt.
/// </summary>
public class DebtPaymentItem
{
    /// <summary>
    /// The transaction's id.
    /// </summary>
    [Description("The transaction's id")]
    public Guid TransactionId { get; init; }

    /// <summary>
    /// The transaction's description.
    /// </summary>
    [Description("The transaction's description")]
    public required string Description { get; init; }

    /// <summary>
    /// The account it was paid from.
    /// </summary>
    [Description("The account it was paid from")]
    public required string AccountName { get; init; }

    /// <summary>
    /// What it paid off, negative for money in such as a refund.
    /// </summary>
    [Description("What it paid off, negative for money in")]
    public decimal Amount { get; init; }

    /// <summary>
    /// When it was paid.
    /// </summary>
    [Description("When it was paid")]
    public DateTime PaidAt { get; init; }

    /// <summary>
    /// Who said it pays the debt, a rule or the user.
    /// </summary>
    [Description("Who said it pays off the debt")]
    public TransferDecidedBy DecidedBy { get; init; }

    /// <summary>
    /// The rule that said so, null unless <see cref="DecidedBy"/> is a rule.
    /// </summary>
    [Description("Name of the rule that said so, for a rule's decision")]
    public string? RuleName { get; init; }
}
