using System.ComponentModel;

namespace FinanceTracker.Contracts.Debts;

/// <summary>
/// A debt with the payments linked to it and what it owed over time.
/// </summary>
public class DebtDetail
{
    /// <summary>
    /// The debt.
    /// </summary>
    [Description("The debt")]
    public required Debt Debt { get; init; }

    /// <summary>
    /// The transactions that pay it off, latest first.
    /// </summary>
    [Description("The transactions that pay off the debt, latest first")]
    public IReadOnlyList<DebtPaymentItem> Payments { get; init; } = [];

    /// <summary>
    /// What it owed at its start, each month end since and today, oldest first. From the linked account's daily
    /// balances when it's linked.
    /// </summary>
    [Description("What the debt owed over time, oldest first")]
    public IReadOnlyList<DebtBalancePoint> History { get; init; } = [];
}
