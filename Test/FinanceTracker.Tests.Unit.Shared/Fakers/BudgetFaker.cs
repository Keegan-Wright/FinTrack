using Bogus;
using FinanceTracker.Domain.Budgeting;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

/// <summary>
/// A budget over one made up tag, running from the current month.
/// </summary>
public sealed class BudgetFaker : Faker<Budget>
{
    public BudgetFaker(Guid userId) =>
        CustomInstantiator(f => new Budget(userId, f.Commerce.Department(), [Guid.NewGuid()],
            f.Finance.Amount(50, 500), BudgetMonth.Of(DateOnly.FromDateTime(DateTime.UtcNow)), RolloverMode.None));
}
