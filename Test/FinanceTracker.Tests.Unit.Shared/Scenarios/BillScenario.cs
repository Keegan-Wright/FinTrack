using FinanceTracker.Domain.Bills;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class BillScenario : ScenarioBuilder
{
    public BillScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a bill paid from the account.
    /// </summary>
    /// <param name="build">Builds the bill from the user and account ids, generated when omitted.</param>
    public Task<TestingScenario> WithBill(string billName, string username, string accountName,
        Func<Guid, Guid, Bill>? build = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Guid accountId = Scenario.AccountMappings[accountName];
        Bill bill = build is null ? new BillFaker(userId, accountId).Generate() : build(userId, accountId);

        Scenario.BillMappings.Add(billName, bill.Id);
        return PersistAsync(context => context.Bills.Add(bill));
    }
}
