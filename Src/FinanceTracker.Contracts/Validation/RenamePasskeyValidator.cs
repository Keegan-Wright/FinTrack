using FinanceTracker.Contracts.Profile;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name for the passkey.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<RenamePasskey>>]
public class RenamePasskeyValidator : AbstractValidator<RenamePasskey>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public RenamePasskeyValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Give it a name")
            .MaximumLength(PasskeyNames.MaximumLength).WithMessage("Use no more than 64 characters");
    }
}
