using FinanceTracker.Contracts.Tokens;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name and a known expiry. The password is checked by the server, as only a password user gives one.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<CreateToken>>]
public class CreateTokenValidator : AbstractValidator<CreateToken>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public CreateTokenValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Give it a name")
            .MaximumLength(TokenNames.MaximumLength).WithMessage("Use no more than 64 characters");
        RuleFor(x => x.Expiry).IsInEnum().WithMessage("Choose how long it lasts");
    }
}
