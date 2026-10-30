using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Budgets;

/// <summary>
/// The budget contracts rollover modes map to the domain's by name, so they have to stay the same.
/// </summary>
public class BudgetContractEnumTests : UnitTestBase
{
    [Test]
    public async Task ContractRolloverMode_MatchesTheDomains() =>
        await Assert.That(Enum.GetNames<Contracts.Budgets.RolloverMode>())
            .IsEquivalentTo(Enum.GetNames<Domain.Budgeting.RolloverMode>());
}
