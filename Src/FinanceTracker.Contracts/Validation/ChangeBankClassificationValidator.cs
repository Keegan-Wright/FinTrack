using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the classification.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ChangeBankClassification>>]
public class ChangeBankClassificationValidator : AbstractValidator<ChangeBankClassification>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ChangeBankClassificationValidator()
    {
        RuleFor(x => x.Classification).NotEmpty().WithMessage("Choose a bank classification");
    }
}
