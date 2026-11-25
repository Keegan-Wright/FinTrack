namespace FinanceTracker.Ai.Tools.Budgets;

/// <summary>
/// The user's budgets for a month.
/// </summary>
/// <param name="Start">The month's first day, which follows the user's pay day when they've set one.</param>
/// <param name="End">The month's last day.</param>
/// <param name="Budgets">Each budget.</param>
/// <param name="Unbudgeted">Spending that no budget covers, positive.</param>
/// <param name="Link">The FinTrack page to check it on.</param>
public sealed record BudgetMonth(DateOnly Start, DateOnly End, List<BudgetSummary> Budgets, decimal Unbudgeted,
    string Link)
{
    /// <summary>
    /// The ISO 4217 code of the currency the amounts are in, e.g. GBP.
    /// </summary>
    public string? Currency { get; init; } = ToolCurrency.Code;
}
