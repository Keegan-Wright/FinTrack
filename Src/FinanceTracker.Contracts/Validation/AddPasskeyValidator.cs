using FinanceTracker.Contracts.Profile;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the browser's credential and a name for it.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<AddPasskey>>]
public class AddPasskeyValidator : AbstractValidator<AddPasskey>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public AddPasskeyValidator()
    {
        RuleFor(x => x.CredentialJson).NotEmpty().WithMessage("The passkey wasn't made, try again");
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Give it a name")
            .MaximumLength(PasskeyNames.MaximumLength).WithMessage("Use no more than 64 characters");
    }
}
