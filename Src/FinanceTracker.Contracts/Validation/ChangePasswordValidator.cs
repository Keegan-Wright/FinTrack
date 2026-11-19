using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the current password and a new one of <see cref="PasswordRules"/> length, typed the same twice.
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
        RuleFor(x => x.NewPassword).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("Enter a new password")
            .MinimumLength(PasswordRules.MinimumLength).WithMessage(PasswordRules.TooShort)
            .MaximumLength(PasswordRules.MaximumLength).WithMessage(PasswordRules.TooLong);
        RuleFor(x => x.ConfirmNewPassword).Equal(x => x.NewPassword).WithMessage("The passwords don't match");
    }
}
