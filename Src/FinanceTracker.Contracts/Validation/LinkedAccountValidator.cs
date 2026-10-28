using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a 6 digit sort code and an 8 digit account number, ignoring spaces and dashes.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<LinkedAccount>>]
public class LinkedAccountValidator : AbstractValidator<LinkedAccount>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public LinkedAccountValidator()
    {
        RuleFor(x => x.SortCode)
            .Must(x => IsDigits(x, 6)).WithMessage("Sort code must be 6 digits");

        RuleFor(x => x.Number)
            .Must(x => IsDigits(x, 8)).WithMessage("Account number must be 8 digits");
    }

    private static bool IsDigits(string? value, int length)
    {
        string compact = string.Concat((value ?? string.Empty).Where(static x => x is not (' ' or '-')));
        return compact.Length == length && compact.All(char.IsAsciiDigit);
    }
}
