using FinanceTracker.Contracts.Profile;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the current password and a new one typed the same twice. How strong it must be is Identity's to say.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ChangePassword>>]
public class ChangePasswordValidator : AbstractValidator<ChangePassword>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Enter your current password");
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage("Enter a new password");
        RuleFor(x => x.ConfirmNewPassword).Equal(x => x.NewPassword).WithMessage("The passwords don't match");
    }
}
