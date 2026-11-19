using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name, at least one tag or account, each once, a limit of 0 or more and a rollover mode.
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

        RuleFor(x => x.TagIds)
            .Must(static (budget, tagIds) => tagIds.Count > 0 || budget.AccountIds.Count > 0)
            .WithMessage("Choose at least one tag or account");

        RuleFor(x => x.AccountIds)
            .Must(static x => x.Distinct().Count() == x.Count)
            .WithMessage("Each account can only be chosen once");

        RuleFor(x => x.MonthlyLimit).GreaterThanOrEqualTo(0).WithMessage("Limit must be 0 or more");

        RuleFor(x => x.RolloverMode).IsInEnum().WithMessage("Choose what carries over");
    }
}
