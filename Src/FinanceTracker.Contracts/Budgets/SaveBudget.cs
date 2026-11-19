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
    /// The user's tags it covers, none for all spending on its <see cref="AccountIds"/>. It needs at least one tag or
    /// account.
    /// </summary>
    [Description("The tags the budget covers, none for all spending on its accounts")]
    public List<Guid> TagIds { get; set; } = [];

    /// <summary>
    /// The accounts it covers, none for every account. Any the user sees, theirs or shared with them, cards included.
    /// </summary>
    [Description("The accounts the budget covers, none for every account")]
    public List<Guid> AccountIds { get; set; } = [];

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
