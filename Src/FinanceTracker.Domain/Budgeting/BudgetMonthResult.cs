namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// How a <see cref="Budget"/> is doing in one month, from <see cref="BudgetCalculator"/>.
/// </summary>
/// <param name="Month">The month.</param>
/// <param name="Limit">That month's limit.</param>
/// <param name="CarriedIn">Brought from the month before by the rollover mode, negative when overspending was.</param>
/// <param name="Spent">Money out less refunds, pending included. Negative when refunds outweigh spending.</param>
/// <param name="Pending">How much of <see cref="Spent"/> is still pending.</param>
/// <param name="ExpectedByNow">What spending at a steady pace through the month would have reached by today, all of
/// <see cref="Available"/> for a past month and nothing for a future one.</param>
public sealed record BudgetMonthResult(BudgetMonth Month, decimal Limit, decimal CarriedIn, decimal Spent,
    decimal Pending, decimal ExpectedByNow)
{
    /// <summary>
    /// What can be spent this month, the limit and what was carried in.
    /// </summary>
    public decimal Available => Limit + CarriedIn;

    /// <summary>
    /// What's left, negative when overspent.
    /// </summary>
    public decimal Remaining => Available - Spent;

    /// <summary>
    /// Whether spending is no further along than a steady pace would be by today.
    /// </summary>
    public bool OnPace => Spent <= ExpectedByNow;
}
