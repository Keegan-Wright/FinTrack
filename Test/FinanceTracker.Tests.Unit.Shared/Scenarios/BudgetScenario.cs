using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class BudgetScenario : ScenarioBuilder
{
    public BudgetScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="build">Builds the budget from the user's id, generated when omitted.</param>
    public Task<TestingScenario> WithBudget(string budgetName, string username, Func<Guid, Budget>? build = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Budget budget = build is null ? new BudgetFaker(userId).Generate() : build(userId);

        Scenario.BudgetMappings.Add(budgetName, budget.Id);
        return PersistAsync(context => context.Budgets.Add(budget));
    }
}
