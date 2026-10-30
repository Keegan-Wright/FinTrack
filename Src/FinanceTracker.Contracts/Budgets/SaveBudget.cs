using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// A new budget, or everything about an existing one.
/// </summary>
public class SaveBudget
{
    /// <summary>
    /// The budget's name.
    /// </summary>
    [Description("The budget's name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The user's tags it covers, at least one.
    /// </summary>
    [Description("The tags the budget covers, at least one")]
    public List<Guid> TagIds { get; set; } = [];

    /// <summary>
    /// How much can be spent each month, from the current month on.
    /// </summary>
    [Description("How much can be spent each month, from the current month on")]
    public decimal MonthlyLimit { get; set; }

    /// <summary>
    /// What it carries from one month into the next.
    /// </summary>
    [Description("What the budget carries from one month into the next")]
    public RolloverMode RolloverMode { get; set; } = RolloverMode.None;
}
