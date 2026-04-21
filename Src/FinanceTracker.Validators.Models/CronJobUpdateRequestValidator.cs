using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Automation;
using FluentValidation;

namespace FinanceTracker.Validators.Models;

[Scoped<IValidator<CronJobUpdateRequest>>]
[InjectionCategory(InjectionCategoryType.Validator)]
public class CronJobUpdateRequestValidator : AbstractValidator<CronJobUpdateRequest>
{
    public CronJobUpdateRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.Expression)
            .NotEmpty().WithMessage("Expression is required")
            .Must(ValidateCron).WithMessage("Expression must be a 6-part cron");

        RuleFor(x => x.RetryIntervals)
            .Must(intervals => intervals == null || intervals.All(i => i >= 0))
            .WithMessage("All intervals must be valid non-negative integers");
    }

    private bool ValidateCron(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 6;
    }
}
