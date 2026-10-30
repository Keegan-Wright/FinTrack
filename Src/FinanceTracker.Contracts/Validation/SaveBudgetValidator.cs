using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name, at least one tag, a limit of 0 or more and a rollover mode.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SaveBudget>>]
public class SaveBudgetValidator : AbstractValidator<SaveBudget>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SaveBudgetValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");

        RuleFor(x => x.TagIds).NotEmpty().WithMessage("Choose at least one tag");

        RuleFor(x => x.MonthlyLimit).GreaterThanOrEqualTo(0).WithMessage("Limit must be 0 or more");

        RuleFor(x => x.RolloverMode).IsInEnum().WithMessage("Choose what carries over");
    }
}
