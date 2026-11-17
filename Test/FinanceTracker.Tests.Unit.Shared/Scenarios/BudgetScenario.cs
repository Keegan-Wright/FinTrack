using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds budgets, mapped by name in <see cref="TestingScenario.BudgetMappings"/>.
/// </summary>
public sealed class BudgetScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public BudgetScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a budget for the user.
    /// </summary>
    /// <param name="budgetName">The name to map the budget by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
    /// <param name="build">Builds the budget from the user's id, generated when omitted.</param>
    public Task<TestingScenario> WithBudget(string budgetName, string username, Func<Guid, Budget>? build = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Budget budget = build is null ? new BudgetFaker(userId).Generate() : build(userId);

        Scenario.BudgetMappings.Add(budgetName, budget.Id);
        return PersistAsync(context => context.Budgets.Add(budget));
    }
}
