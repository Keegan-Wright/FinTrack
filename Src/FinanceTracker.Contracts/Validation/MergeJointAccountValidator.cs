using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires both copies.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<MergeJointAccount>>]
public class MergeJointAccountValidator : AbstractValidator<MergeJointAccount>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public MergeJointAccountValidator()
    {
        RuleFor(static x => x.YourAccountId).NotEmpty().WithMessage("Choose your copy of the account");
        RuleFor(static x => x.TheirAccountId).NotEmpty().WithMessage("Choose their copy of the account");
    }
}
