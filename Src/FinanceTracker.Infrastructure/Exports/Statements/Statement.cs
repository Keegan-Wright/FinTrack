using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Infrastructure.Exports.Statements;

/// <summary>
/// Everything on a statement for one pay period, across all of the user's accounts, gathered by
/// <see cref="StatementBuilder"/> and laid out by <see cref="StatementDocument"/>.
/// </summary>
public sealed class Statement
{
    /// <summary>
    /// The first of the month the period is named after.
    /// </summary>
    public required DateOnly Month { get; init; }

    /// <summary>
    /// The first day, the pay day or the 1st for a calendar month.
    /// </summary>
    public required DateOnly Start { get; init; }

    /// <summary>
    /// The last day, inclusive.
    /// </summary>
    public required DateOnly End { get; init; }

    /// <summary>
    /// Whether the period is still running, so its figures are so far.
    /// </summary>
    public required bool IsCurrent { get; init; }

    /// <summary>
    /// Whether the period runs from pay day, starting before the 1st of the month it's named after, rather than being
    /// a calendar month.
    /// </summary>
    public bool FromPayDay => Start != Month;

    /// <summary>
    /// The day it was made, where the user is, which goals and debts stand at.
    /// </summary>
    public required DateOnly MadeOn { get; init; }

    /// <summary>
    /// Money in and out, transfers between the user's own accounts or within their household kept apart.
    /// </summary>
    public required SpentInTimePeriod Totals { get; init; }

    /// <summary>
    /// Money out by tag, most first. A transaction with more than one tag counts towards each.
    /// </summary>
    public required IReadOnlyList<SpentInCategoryReport> Tags { get; init; }

    public required BillMonthOverview Bills { get; init; }

    public required BudgetMonthOverview Budgets { get; init; }

    /// <summary>
    /// As they stand on <see cref="MadeOn"/>.
    /// </summary>
    public required IReadOnlyList<SavingsGoal> SavingsGoals { get; init; }

    /// <summary>
    /// As they stand on <see cref="MadeOn"/>.
    /// </summary>
    public required IReadOnlyList<Debt> Debts { get; init; }

    /// <summary>
    /// Every transaction in the period, oldest first.
    /// </summary>
    public required IReadOnlyList<Transaction> Transactions { get; init; }
}
