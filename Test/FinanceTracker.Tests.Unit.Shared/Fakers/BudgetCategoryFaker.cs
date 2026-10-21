using Bogus;
using FinanceTracker.Domain.Budgeting;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class BudgetCategoryFaker : Faker<BudgetCategory>
{
    public BudgetCategoryFaker(Guid userId) =>
        CustomInstantiator(f => new BudgetCategory(userId,
            f.Commerce.Department(),
            f.Finance.Amount(0, 1000),
            f.Finance.Amount(0, 500),
            f.Finance.Amount(0, 10000),
            f.Date.Future(1, DateTime.UtcNow)));
}
