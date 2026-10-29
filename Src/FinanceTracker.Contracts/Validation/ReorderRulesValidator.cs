using FinanceTracker.Contracts.Rules;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires at least one rule.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ReorderRules>>]
public class ReorderRulesValidator : AbstractValidator<ReorderRules>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ReorderRulesValidator()
    {
        RuleFor(x => x.RuleIds).NotEmpty().WithMessage("There are no rules to order");

        RuleFor(x => x.RuleIds)
            .Must(x => x.Distinct().Count() == x.Count)
            .WithMessage("Each rule can only appear once");
    }
}
