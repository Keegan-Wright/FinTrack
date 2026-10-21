using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class DebtScenario : ScenarioBuilder
{
    public DebtScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="create">Builds the debt for the owning user id, generated when omitted.</param>
    public Task<TestingScenario> WithDebt(string debtName, string username, Func<Guid, Debt>? create = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Debt debt = create?.Invoke(userId) ?? new DebtFaker(userId).Generate();

        Scenario.DebtMappings.Add(debtName, debt.Id);
        return PersistAsync(context => context.Debts.Add(debt));
    }
}
