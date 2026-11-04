using FinanceTracker.Contracts.Automation;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the job, a description, a Quartz cron expression's shape and retry intervals of 0 or more. The server
/// reads the expression with Quartz itself, so one that has the shape but can't be read is refused there.
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
        RuleFor(x => x.Function)
            .NotEmpty().WithMessage("The job is required");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.Expression)
            .Must(ValidateCron)
            .WithMessage("Expression must be a Quartz cron: seconds, minutes, hours, day of month, month and day of " +
                         "week, with ? in one of the two day fields, e.g. 0 0 */4 * * ?");

        RuleFor(x => x.RetryIntervals)
            .Must(intervals => intervals == null || intervals.All(i => i >= 0))
            .WithMessage("All intervals must be valid non-negative integers");
    }

    /// <summary>
    /// 6 parts, or 7 with a year, and <c>?</c> in exactly one of the day of month and day of week fields, as Quartz
    /// needs.
    /// </summary>
    private static bool ValidateCron(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        string[] parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (6 or 7))
        {
            return false;
        }

        bool dayOfMonthUnset = parts[3] == "?";
        bool dayOfWeekUnset = parts[5] == "?";
        return dayOfMonthUnset != dayOfWeekUnset;
    }
}
