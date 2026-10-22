using FinanceTracker.Contracts.Classifications;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a tag.
/// </summary>
public class AddClassificationsRequestValidator : AbstractValidator<AddClassifications>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public AddClassificationsRequestValidator()
    {
        RuleFor(x => x.Tag)
            .NotEmpty().WithMessage("Classification is required");
    }
}
