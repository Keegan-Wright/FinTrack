using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Infrastructure.Validation;

[Scoped<IValidator<AddHouseholdMember>>]
[InjectionCategory(InjectionCategoryType.Validator)]
public class AddHouseholdMemberRequestValidator : AbstractValidator<AddHouseholdMember>
{
    public AddHouseholdMemberRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First Name is required");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last Name is required");

        RuleFor(x => x.Income)
            .GreaterThanOrEqualTo(0).WithMessage("Income must be 0 or greater");
    }
}
