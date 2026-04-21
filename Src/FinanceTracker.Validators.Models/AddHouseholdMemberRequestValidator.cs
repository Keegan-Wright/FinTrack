using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.HouseholdMember;
using FluentValidation;

namespace FinanceTracker.Validators.Models;

[Scoped<IValidator<AddHouseholdMemberRequest>>]
[InjectionCategory(InjectionCategoryType.Validator)]
public class AddHouseholdMemberRequestValidator : AbstractValidator<AddHouseholdMemberRequest>
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
