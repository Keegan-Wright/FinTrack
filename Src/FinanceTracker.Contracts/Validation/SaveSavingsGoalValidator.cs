using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name, amounts of 0 or more, and what its source needs: an account, or tags and a day to count from.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SaveSavingsGoal>>]
public class SaveSavingsGoalValidator : AbstractValidator<SaveSavingsGoal>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SaveSavingsGoalValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");

        RuleFor(x => x.Target).GreaterThanOrEqualTo(0).WithMessage("Target must be 0 or more");

        RuleFor(x => x.MonthlyContribution).GreaterThanOrEqualTo(0)
            .WithMessage("Monthly contribution must be 0 or more");

        RuleFor(x => x.Saved).GreaterThanOrEqualTo(0).WithMessage("Saved must be 0 or more");

        RuleFor(x => x.Source).IsInEnum().WithMessage("Choose where the saved amount comes from");

        RuleFor(x => x.AccountId)
            .NotEmpty()
            .When(x => x.Source == SavingsGoalSource.Account)
            .WithMessage("Choose an account");

        RuleFor(x => x.TagIds)
            .NotEmpty()
            .When(x => x.Source == SavingsGoalSource.Tags)
            .WithMessage("Choose at least one tag");

        RuleFor(x => x.TrackedFrom)
            .NotNull()
            .When(x => x.Source == SavingsGoalSource.Tags)
            .WithMessage("Choose the day to count from");
    }
}
