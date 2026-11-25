using System.ComponentModel;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Ai.Tools.Budgets;

/// <summary>
/// Tools for the user's budgets.
/// </summary>
/// <param name="budgetsService">Reads the budgets, the same as the budgets page.</param>
/// <param name="pageLinks">Makes the links to the pages its results can be checked on.</param>
public sealed class BudgetTools(IBudgetsService budgetsService, PageLinks pageLinks)
{
    /// <summary>
    /// How each budget is doing in a month.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_budgets")]
    [Description("Shows how each of the user's budgets is doing in a month: what's available, spent and left, " +
        "and whether spending is on pace.")]
    public async Task<BudgetMonth> GetBudgetsAsync(
        [Description("The year, such as 2026.")] int year,
        [Description("The month, 1 to 12.")] int month,
        CancellationToken cancellationToken = default)
    {
        BudgetMonthOverview overview = await budgetsService.GetMonthAsync(year, month, cancellationToken);
        List<BudgetSummary> budgets = overview.Budgets
            .Select(static x => new BudgetSummary(x.Name, x.Available, x.Spent, x.Remaining, x.OnPace))
            .ToList();
        return new BudgetMonth(overview.Start, overview.End, budgets, overview.Unbudgeted,
            pageLinks.For(ToolLinks.Budgets));
    }
}
