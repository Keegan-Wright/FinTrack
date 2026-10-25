using FinanceTracker.Contracts.Auth;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a username and password.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<LoginRequest>>]
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
    }
}
