using FinanceTracker.Domain.Debts;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds debts, mapped by name in <see cref="TestingScenario.DebtMappings"/>.
/// </summary>
public sealed class DebtScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public DebtScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a debt for the user.
    /// </summary>
    /// <param name="debtName">The name to map the debt by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
    /// <param name="create">Builds the debt for the owning user id, generated when omitted.</param>
    public Task<TestingScenario> WithDebt(string debtName, string username, Func<Guid, Debt>? create = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Debt debt = create?.Invoke(userId) ?? new DebtFaker(userId).Generate();

        Scenario.DebtMappings.Add(debtName, debt.Id);
        return PersistAsync(context => context.Debts.Add(debt));
    }
}
