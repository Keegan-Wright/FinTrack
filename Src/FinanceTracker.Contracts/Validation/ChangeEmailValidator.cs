using FinanceTracker.Contracts.Profile;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires an email address.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ChangeEmail>>]
public class ChangeEmailValidator : AbstractValidator<ChangeEmail>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ChangeEmailValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().WithMessage("Enter an email")
            .EmailAddress().WithMessage("Enter an email address, like name@example.com");
    }
}
