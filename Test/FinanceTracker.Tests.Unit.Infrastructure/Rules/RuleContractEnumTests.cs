using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Rules;

/// <summary>
/// The rule contracts enums map to the domain's by name, so they have to stay the same.
/// </summary>
public class RuleContractEnumTests : UnitTestBase
{
    [Test]
    [Arguments(typeof(Contracts.Rules.RuleField), typeof(Domain.Rules.RuleField))]
    [Arguments(typeof(Contracts.Rules.RuleOperator), typeof(Domain.Rules.RuleOperator))]
    [Arguments(typeof(Contracts.Rules.RuleMatchMode), typeof(Domain.Rules.RuleMatchMode))]
    [Arguments(typeof(Contracts.Rules.RuleActionType), typeof(Domain.Rules.RuleActionType))]
    public async Task ContractEnum_MatchesTheDomains(Type contract, Type domain) =>
        await Assert.That(Enum.GetNames(contract)).IsEquivalentTo(Enum.GetNames(domain));
}
