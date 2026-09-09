using FinanceTracker.Contracts.Automation;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Infrastructure.Validation;

[Scoped<IValidator<UpdateCronJob>>]
[InjectionCategory(InjectionCategoryType.Validator)]
public class CronJobUpdateRequestValidator : AbstractValidator<UpdateCronJob>
{
    public CronJobUpdateRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.Expression)
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
