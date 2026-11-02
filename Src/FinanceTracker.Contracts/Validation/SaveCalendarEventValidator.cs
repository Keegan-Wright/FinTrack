using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a title and a date, a savings target over 0 when set, and one when a goal is created. A goal is either
/// chosen or created, and only a yearly event with one moves it on.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SaveCalendarEvent>>]
public class SaveCalendarEventValidator : AbstractValidator<SaveCalendarEvent>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SaveCalendarEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required");

        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Choose a date");

        RuleFor(x => x.Recurrence).IsInEnum();

        RuleFor(x => x.SavingsTarget)
            .GreaterThan(0)
            .When(x => x.SavingsTarget is not null)
            .WithMessage("What to save must be more than 0");

        RuleFor(x => x.SavingsTarget)
            .NotNull()
            .When(x => x.CreateSavingsGoal)
            .WithMessage("Say what to save to create a goal for it");

        RuleFor(x => x.SavingsGoalId)
            .Null()
            .When(x => x.CreateSavingsGoal)
            .WithMessage("Choose a goal or create one, not both");

        RuleFor(x => x.RollsGoalOn)
            .Equal(false)
            .When(x => x.Recurrence != EventRecurrence.Yearly)
            .WithMessage("Only a yearly event can move its goal on");

        RuleFor(x => x.RollsGoalOn)
            .Equal(false)
            .When(x => x.SavingsGoalId is null && !x.CreateSavingsGoal)
            .WithMessage("Choose or create a goal to move on");
    }
}
