using FinanceTracker.Contracts.Profile;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a six digit code, spaces allowed, and the user's password.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<EnableAuthenticator>>]
public class EnableAuthenticatorValidator : AbstractValidator<EnableAuthenticator>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public EnableAuthenticatorValidator()
    {
        RuleFor(x => x.Code)
            .Must(static x => TwoFactorCodes.IsCode(x))
            .WithMessage("Enter the 6 digit code from your app");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Enter your password");
    }
}
