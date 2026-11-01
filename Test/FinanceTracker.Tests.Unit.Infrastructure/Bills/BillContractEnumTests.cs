using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Bills;

/// <summary>
/// The bill contracts' enums map to the domain's by name, so they have to stay the same.
/// </summary>
public class BillContractEnumTests : UnitTestBase
{
    [Test]
    public async Task ContractBillSource_MatchesTheDomains() =>
        await Assert.That(Enum.GetNames<Contracts.Bills.BillSource>())
            .IsEquivalentTo(Enum.GetNames<Domain.Bills.BillSource>());

    [Test]
    public async Task ContractBillFrequency_MatchesTheDomains() =>
        await Assert.That(Enum.GetNames<Contracts.Bills.BillFrequency>())
            .IsEquivalentTo(Enum.GetNames<Domain.Bills.BillFrequency>());

    [Test]
    public async Task ContractBillState_MatchesTheDomains() =>
        await Assert.That(Enum.GetNames<Contracts.Bills.BillState>())
            .IsEquivalentTo(Enum.GetNames<Domain.Bills.BillState>());
}
