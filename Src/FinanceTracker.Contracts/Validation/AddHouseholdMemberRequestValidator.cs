using FinanceTracker.Contracts.HouseholdMember;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires both names and an income of 0 or more.
/// </summary>
public class AddHouseholdMemberRequestValidator : AbstractValidator<AddHouseholdMember>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
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
