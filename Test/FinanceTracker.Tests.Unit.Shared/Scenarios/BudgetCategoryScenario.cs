using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class BudgetCategoryScenario : ScenarioBuilder
{
    public BudgetCategoryScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="create">Builds the category for the owning user id, generated when omitted.</param>
    public Task<TestingScenario> WithBudgetCategory(string budgetCategoryName, string username,
        Func<Guid, BudgetCategory>? create = null)
    {
        Guid userId = Scenario.UserMappings[username];
        BudgetCategory budgetCategory = create?.Invoke(userId) ?? new BudgetCategoryFaker(userId).Generate();

        Scenario.BudgetCategoryMappings.Add(budgetCategoryName, budgetCategory.Id);
        return PersistAsync(context => context.BudgetCategories.Add(budgetCategory));
    }
}
