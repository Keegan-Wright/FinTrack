using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Keeps each alert setting to what it allows, and each account to one amount.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ChangeAlertSettings>>]
public class ChangeAlertSettingsValidator : AbstractValidator<ChangeAlertSettings>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ChangeAlertSettingsValidator()
    {
        RuleFor(x => x.BudgetWarningPercent)
            .InclusiveBetween(1, 100).WithMessage("Enter a percentage from 1 to 100");

        RuleFor(x => x.BillReminderDays)
            .InclusiveBetween(1, ChangeAlertSettings.MostBillReminderDays)
            .WithMessage($"Enter 1 to {ChangeAlertSettings.MostBillReminderDays} days");

        RuleFor(x => x.LargeTransactionOver)
            .GreaterThan(0).When(static x => x.LargeTransactionOver is not null)
            .WithMessage("Enter an amount over 0, or leave it empty to turn it off");

        RuleFor(x => x.LowBalance)
            .NotNull()
            .Must(BeOneEach).WithMessage("Each account can have one amount");
    }

    private static bool BeOneEach(List<LowBalanceThreshold>? thresholds) =>
        thresholds is null || thresholds.Select(static x => x.AccountId).Distinct().Count() == thresholds.Count;
}
