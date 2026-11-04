using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.PayPeriods;

/// <summary>
/// The contract's pay day kinds are cast from the domain's rules, so their names and values have to stay the same.
/// </summary>
public class PayContractEnumTests : UnitTestBase
{
    [Test]
    public async Task ContractPayDayKind_MatchesTheDomainsRule()
    {
        using (Assert.Multiple())
        {
            await Assert.That(Enum.GetNames<Contracts.PayPeriods.PayDayKind>())
                .IsEquivalentTo(Enum.GetNames<Domain.PayPeriods.PayDayRule>());
            await Assert.That(Enum.GetValues<Contracts.PayPeriods.PayDayKind>().Select(static x => (int)x))
                .IsEquivalentTo(Enum.GetValues<Domain.PayPeriods.PayDayRule>().Select(static x => (int)x));
        }
    }
}
