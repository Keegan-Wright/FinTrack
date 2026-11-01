using FinanceTracker.Contracts.Bills;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name, a payee, an account, a frequency, a due date, an amount over 0 and a tolerance from 0 to 100%.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SaveBill>>]
public class SaveBillValidator : AbstractValidator<SaveBill>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SaveBillValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");

        RuleFor(x => x.Payee).NotEmpty().WithMessage("Enter who it's paid to, as it reads on a statement");

        RuleFor(x => x.AccountId).NotEmpty().WithMessage("Choose the account it's paid from");

        RuleFor(x => x.Frequency).IsInEnum().WithMessage("Choose how often it's due");

        RuleFor(x => x.NextDue).NotEqual(default(DateOnly)).WithMessage("Choose when it's next due");

        RuleFor(x => x.ExpectedAmount).GreaterThan(0).WithMessage("Amount must be more than 0");

        RuleFor(x => x.TolerancePercent).InclusiveBetween(0, 100).WithMessage("Tolerance must be from 0 to 100%");
    }
}
