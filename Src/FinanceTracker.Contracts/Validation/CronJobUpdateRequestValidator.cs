using FinanceTracker.Contracts.Automation;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a description, a 6 part cron expression and retry intervals of 0 or more.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<UpdateCronJob>>]
public class CronJobUpdateRequestValidator : AbstractValidator<UpdateCronJob>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
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
        string[] parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 6;
    }
}
