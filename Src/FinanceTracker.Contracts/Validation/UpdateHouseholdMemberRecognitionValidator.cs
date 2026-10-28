using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires every payee name to have text and every account to pass <see cref="LinkedAccountValidator"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<UpdateHouseholdMemberRecognition>>]
public class UpdateHouseholdMemberRecognitionValidator : AbstractValidator<UpdateHouseholdMemberRecognition>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public UpdateHouseholdMemberRecognitionValidator()
    {
        RuleFor(x => x.PayeeNames).NotNull();
        RuleForEach(x => x.PayeeNames)
            .NotEmpty().WithMessage("A name can't be blank")
            .MaximumLength(200).WithMessage("A name can be at most 200 characters");

        RuleFor(x => x.LinkedAccounts).NotNull();
        RuleForEach(x => x.LinkedAccounts).SetValidator(new LinkedAccountValidator());
    }
}
