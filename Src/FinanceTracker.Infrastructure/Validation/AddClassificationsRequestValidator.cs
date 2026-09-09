using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Infrastructure.Validation;

[Scoped<IValidator<AddClassificationsRequest>>]
[InjectionCategory(InjectionCategoryType.Validator)]
public class AddClassificationsRequestValidator : AbstractValidator<AddClassificationsRequest>
{
    public AddClassificationsRequestValidator()
    {
        RuleFor(x => x.Tag)
            .NotEmpty().WithMessage("Classification is required");
    }
}
