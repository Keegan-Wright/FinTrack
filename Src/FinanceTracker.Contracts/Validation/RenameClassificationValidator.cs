using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a tag.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<RenameClassification>>]
public class RenameClassificationValidator : AbstractValidator<RenameClassification>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public RenameClassificationValidator()
    {
        RuleFor(x => x.Tag)
            .NotEmpty().WithMessage("Classification is required");
    }
}
