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
    /// <param name="sharing">How it's shared with the user, none for their own unshared one.</param>
    public static Budget ToContract(DomainBudget budget, BudgetMonth currentMonth, BudgetSharing? sharing = null) =>
        new()
        {
            Id = budget.Id,
            Name = budget.Name,
            TagIds = budget.TagIds.ToList(),
            RolloverMode = Enum.Parse<RolloverMode>(budget.RolloverMode.ToString()),
            MonthlyLimit = budget.LimitFor(currentMonth) ?? budget.Limits[^1].Amount,
            Limits = budget.Limits.Select(static x => new BudgetLimit { From = x.From, Amount = x.Amount }).ToList(),
            SharedBy = sharing?.SharedBy,
            CanChange = sharing?.CanChange ?? true,
            IsHousehold = sharing?.IsHousehold ?? false,
            TagNames = sharing?.TagNames ?? [],
            TagChoices = sharing?.TagChoices ?? []
        };

    public static BudgetStatus ToContract(DomainBudget budget, BudgetMonthResult result,
        BudgetSharing? sharing = null) => new()
    {
        SharedBy = sharing?.SharedBy,
        IsHousehold = sharing?.IsHousehold ?? false,
        CanChange = sharing?.CanChange ?? true,
        BudgetId = budget.Id,
        Name = budget.Name,
        CoversNothing = budget.CoversNothing,
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
