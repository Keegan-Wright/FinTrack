using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a transaction and at least one classification, all with ids.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<AddCustomClassificationsToTransaction>>]
public class AddCustomClassificationsToTransactionRequestValidator : AbstractValidator<AddCustomClassificationsToTransaction>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public AddCustomClassificationsToTransactionRequestValidator()
    {
        RuleFor(x => x.TransactionId)
            .NotEqual(Guid.Empty).WithMessage("Invalid transaction id");

        RuleFor(x => x.Classifications)
            .NotEmpty().WithMessage("At least one classification must be selected");

        RuleForEach(x => x.Classifications).ChildRules(classification =>
        {
            classification.RuleFor(x => x.ClassificationId)
                .NotEqual(Guid.Empty)
                .WithMessage("Classification {CollectionIndex} is invalid");
        });
    }
}
