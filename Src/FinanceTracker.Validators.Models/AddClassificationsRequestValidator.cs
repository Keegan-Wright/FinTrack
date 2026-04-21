using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Classifications;
using FluentValidation;

namespace FinanceTracker.Validators.Models;

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
