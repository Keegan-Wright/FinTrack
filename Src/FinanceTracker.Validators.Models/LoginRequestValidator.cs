using FinanceTracker.Generated.Enums;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Models.Request.Auth;
using FluentValidation;

namespace FinanceTracker.Validators.Models;

[Scoped<IValidator<LoginRequest>>]
[InjectionCategory(InjectionCategoryType.Validator)]
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
    }
}