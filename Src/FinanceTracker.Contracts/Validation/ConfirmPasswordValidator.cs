using FinanceTracker.Contracts.Profile;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the user's password.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ConfirmPassword>>]
public class ConfirmPasswordValidator : AbstractValidator<ConfirmPassword>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ConfirmPasswordValidator() =>
        RuleFor(x => x.Password).NotEmpty().WithMessage("Enter your password");
}
