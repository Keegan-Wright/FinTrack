using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Domain.Budgeting;
using Budget = FinanceTracker.Contracts.Budgets.Budget;
using BudgetLimit = FinanceTracker.Contracts.Budgets.BudgetLimit;
using DomainBudget = FinanceTracker.Domain.Budgeting.Budget;
using DomainRolloverMode = FinanceTracker.Domain.Budgeting.RolloverMode;
using RolloverMode = FinanceTracker.Contracts.Budgets.RolloverMode;

namespace FinanceTracker.Infrastructure.Budgets;

/// <summary>
/// Between the domain's budgets and their contracts. The rollover modes share names, so they map by name.
/// </summary>
internal static class BudgetMapper
{
    /// <param name="currentMonth">The month whose limit is shown as the budget's monthly limit.</param>
    public static Budget ToContract(DomainBudget budget, BudgetMonth currentMonth) => new()
    {
        Id = budget.Id,
        Name = budget.Name,
        TagIds = budget.TagIds.ToList(),
        RolloverMode = Enum.Parse<RolloverMode>(budget.RolloverMode.ToString()),
        MonthlyLimit = budget.LimitFor(currentMonth) ?? budget.Limits[^1].Amount,
        Limits = budget.Limits.Select(static x => new BudgetLimit { From = x.From, Amount = x.Amount }).ToList()
    };

    public static BudgetStatus ToContract(DomainBudget budget, BudgetMonthResult result) => new()
    {
        BudgetId = budget.Id,
        Name = budget.Name,
        Limit = result.Limit,
        CarriedIn = result.CarriedIn,
        Available = result.Available,
        Spent = result.Spent,
        Pending = result.Pending,
        Remaining = result.Remaining,
        ExpectedByNow = result.ExpectedByNow,
        OnPace = result.OnPace
    };

    public static DomainRolloverMode ToDomain(RolloverMode rolloverMode) =>
        Enum.Parse<DomainRolloverMode>(rolloverMode.ToString());
}
