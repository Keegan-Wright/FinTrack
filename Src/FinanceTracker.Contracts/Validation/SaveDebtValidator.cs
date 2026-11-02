using FinanceTracker.Contracts.Debts;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name, a starting balance and monthly payment over 0, a start date, a rate from 0 to 100% and a pay-off
/// goal, when set, after the start.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SaveDebt>>]
public class SaveDebtValidator : AbstractValidator<SaveDebt>
{
    /// <summary>
    /// The highest rate taken, matching the domain's.
    /// </summary>
    public const decimal MaximumInterestRate = 100;

    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SaveDebtValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");

        RuleFor(x => x.StartingBalance).GreaterThan(0).WithMessage("What was owed must be more than 0");

        RuleFor(x => x.StartedOn).NotEqual(default(DateOnly)).WithMessage("Choose when that was owed");

        RuleFor(x => x.InterestRate)
            .InclusiveBetween(0, MaximumInterestRate)
            .WithMessage("Interest rate must be from 0 to 100%");

        RuleFor(x => x.MonthlyPayment).GreaterThan(0).WithMessage("Monthly payment must be more than 0");

        RuleFor(x => x.PayOffGoal)
            .GreaterThan(x => x.StartedOn)
            .When(x => x.PayOffGoal is not null)
            .WithMessage("The pay-off goal must be after the debt started");
    }
}
