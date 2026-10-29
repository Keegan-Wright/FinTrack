using Bogus;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

/// <summary>
/// An enabled rule looking for a word in the description that marks a match as not a transfer.
/// </summary>
public sealed class RuleFaker : Faker<Rule>
{
    public RuleFaker(Guid userId) =>
        CustomInstantiator(f => new Rule(userId, f.Commerce.Department(), f.Random.Int(0, 20), RuleMatchMode.All,
            [new RuleCondition(RuleField.Description, RuleOperator.Contains, f.Commerce.ProductMaterial())],
            [RuleAction.MarkNotATransfer()]));
}
