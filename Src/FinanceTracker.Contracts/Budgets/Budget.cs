using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// One of the user's budgets, a monthly limit on what their tags cover.
/// </summary>
public class Budget
{
    /// <summary>
    /// The budget's id.
    /// </summary>
    [Description("Unique identifier for the budget")]
    public Guid Id { get; init; }

    /// <summary>
    /// The budget's name.
    /// </summary>
    [Description("The budget's name")]
    public required string Name { get; init; }

    /// <summary>
    /// The user's tags it covers, a transaction with any of them counts towards it.
    /// </summary>
    [Description("The tags the budget covers")]
    public List<Guid> TagIds { get; init; } = [];

    /// <summary>
    /// What it carries from one month into the next.
    /// </summary>
    [Description("What the budget carries from one month into the next")]
    public RolloverMode RolloverMode { get; init; }

    /// <summary>
    /// The limit for the current month.
    /// </summary>
    [Description("The limit for the current month")]
    public decimal MonthlyLimit { get; init; }

    /// <summary>
    /// Every limit it's had, oldest first, so past months show the limit they had.
    /// </summary>
    [Description("Every limit the budget has had, oldest first")]
    public List<BudgetLimit> Limits { get; init; } = [];
}
