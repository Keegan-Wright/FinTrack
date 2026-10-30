using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// How one budget is doing in one month.
/// </summary>
public class BudgetStatus
{
    /// <summary>
    /// The budget's id.
    /// </summary>
    [Description("The budget's id")]
    public Guid BudgetId { get; init; }

    /// <summary>
    /// The budget's name.
    /// </summary>
    [Description("The budget's name")]
    public required string Name { get; init; }

    /// <summary>
    /// Whether all of its tags were deleted, so it covers nothing until it's given one.
    /// </summary>
    [Description("Whether all of the budget's tags were deleted, so it covers nothing")]
    public bool CoversNothing { get; init; }

    /// <summary>
    /// That month's limit.
    /// </summary>
    [Description("That month's limit")]
    public decimal Limit { get; init; }

    /// <summary>
    /// Brought from the month before by the rollover mode, negative when overspending was.
    /// </summary>
    [Description("Brought from the month before, negative when overspending was")]
    public decimal CarriedIn { get; init; }

    /// <summary>
    /// What can be spent this month, the limit and what was carried in.
    /// </summary>
    [Description("What can be spent this month")]
    public decimal Available { get; init; }

    /// <summary>
    /// Money out less refunds, pending included, never below 0.
    /// </summary>
    [Description("Money out less refunds, pending included")]
    public decimal Spent { get; init; }

    /// <summary>
    /// How much of <see cref="Spent"/> is still pending.
    /// </summary>
    [Description("How much of what's spent is still pending")]
    public decimal Pending { get; init; }

    /// <summary>
    /// What's left, negative when overspent.
    /// </summary>
    [Description("What's left, negative when overspent")]
    public decimal Remaining { get; init; }

    /// <summary>
    /// What spending at a steady pace through the month would have reached by today.
    /// </summary>
    [Description("What spending at a steady pace would have reached by today")]
    public decimal ExpectedByNow { get; init; }

    /// <summary>
    /// Whether spending is no further along than a steady pace would be.
    /// </summary>
    [Description("Whether spending is no further along than a steady pace")]
    public bool OnPace { get; init; }
}
