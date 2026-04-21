using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Classifications;
using FluentValidation;

namespace FinanceTracker.Validators.Models;

[Scoped<IValidator<AddCustomClassificationsToTransactionRequest>>]
[InjectionCategory(InjectionCategoryType.Validator)]
public class AddCustomClassificationsToTransactionRequestValidator : AbstractValidator<AddCustomClassificationsToTransactionRequest>
{
    public AddCustomClassificationsToTransactionRequestValidator()
    {
        RuleFor(x => x.Classifications)
            .NotEmpty().WithMessage("At least one classification must be selected");
    }
}
