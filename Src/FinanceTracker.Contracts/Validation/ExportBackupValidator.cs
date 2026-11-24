using FinanceTracker.Contracts.Exports;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// A plain backup needs nothing. A protected one needs a password of <see cref="BackupPasswordRules"/> length, typed
/// the same twice.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ExportBackup>>]
public class ExportBackupValidator : AbstractValidator<ExportBackup>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ExportBackupValidator()
    {
        When(static x => x.Password is not null, () =>
        {
            RuleFor(x => x.Password).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Enter a password")
                .MinimumLength(BackupPasswordRules.MinimumLength).WithMessage(BackupPasswordRules.TooShort)
                .MaximumLength(BackupPasswordRules.MaximumLength).WithMessage(BackupPasswordRules.TooLong);
            RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("The passwords don't match");
        });
    }
}
