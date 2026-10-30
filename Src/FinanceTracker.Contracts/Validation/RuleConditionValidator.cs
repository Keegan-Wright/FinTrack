using System.Globalization;
using System.Text.RegularExpressions;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires an operator that suits the field and values that suit both: text operators on text fields with a regular
/// expression that compiles, amounts written with a <c>.</c> and a range lowest first, <c>Debit</c> or <c>Credit</c>
/// for a type and an id for an account. Only <see cref="RuleOperator.Between"/> takes a second value.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<RuleCondition>>]
public class RuleConditionValidator : AbstractValidator<RuleCondition>
{
    private static readonly RuleOperator[] TextOperators =
    [
        RuleOperator.Equals, RuleOperator.Contains, RuleOperator.StartsWith, RuleOperator.EndsWith,
        RuleOperator.Regex
    ];

    private static readonly RuleOperator[] AmountOperators =
        [RuleOperator.Equals, RuleOperator.GreaterThan, RuleOperator.LessThan, RuleOperator.Between];

    private static readonly RuleOperator[] EqualsOnly = [RuleOperator.Equals];

    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public RuleConditionValidator()
    {
        RuleFor(x => x.Field).IsInEnum().WithMessage("Choose what to look at");

        RuleFor(x => x.Operator)
            .Must((condition, @operator) => AllowedOperators(condition.Field).Contains(@operator))
            .WithMessage(x => $"{x.Operator} can't be used on {x.Field}");

        RuleFor(x => x.Value).NotEmpty().WithMessage("Value is required");

        RuleFor(x => x.Value)
            .Must((condition, value) => IsValidRegex(value, condition.IgnoreCase))
            .When(x => IsText(x.Field) && x.Operator == RuleOperator.Regex && !string.IsNullOrWhiteSpace(x.Value))
            .WithMessage("Value isn't a valid regular expression");

        RuleFor(x => x.Value)
            .Must(x => TryParseAmount(x, out _))
            .When(x => x.Field == RuleField.Amount && !string.IsNullOrWhiteSpace(x.Value))
            .WithMessage("Value must be an amount, e.g. 12.50");

        RuleFor(x => x.Value)
            .Must(IsTransactionType)
            .When(x => x.Field == RuleField.Type && !string.IsNullOrWhiteSpace(x.Value))
            .WithMessage("Value must be Debit or Credit");

        RuleFor(x => x.Value)
            .Must(x => Guid.TryParse(x, out _))
            .When(x => x.Field == RuleField.Account && !string.IsNullOrWhiteSpace(x.Value))
            .WithMessage("Choose an account");

        RuleFor(x => x.SecondValue)
            .Empty()
            .When(x => x.Operator != RuleOperator.Between)
            .WithMessage("Only between takes a second value");

        RuleFor(x => x.SecondValue)
            .Must((condition, secondValue) => IsRangeTop(condition.Value, secondValue))
            .When(x => x.Field == RuleField.Amount && x.Operator == RuleOperator.Between)
            .WithMessage("Second value must be an amount no smaller than the first");
    }

    private static RuleOperator[] AllowedOperators(RuleField field)
    {
        if (IsText(field))
        {
            return TextOperators;
        }

        if (field == RuleField.Amount)
        {
            return AmountOperators;
        }

        return EqualsOnly;
    }

    private static bool IsText(RuleField field) => field is RuleField.Description or RuleField.Category
        or RuleField.ProviderCategory or RuleField.ProviderClassification or RuleField.Counterparty;

    private static bool IsValidRegex(string value, bool ignoreCase)
    {
        RegexOptions options = ignoreCase
            ? RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
            : RegexOptions.CultureInvariant;

        try
        {
            _ = new Regex(value, options);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsTransactionType(string value) =>
        string.Equals(value, "Debit", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "Credit", StringComparison.OrdinalIgnoreCase);

    private static bool IsRangeTop(string value, string? secondValue)
    {
        if (!TryParseAmount(secondValue, out decimal top))
        {
            return false;
        }

        // A bad first value has its own message
        return !TryParseAmount(value, out decimal bottom) || top >= bottom;
    }

    /// <summary>
    /// Digits and a <c>.</c> only, as the domain reads them, so <c>12,50</c> is refused rather than read as 1250.
    /// </summary>
    private static bool TryParseAmount(string? value, out decimal amount) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
}
