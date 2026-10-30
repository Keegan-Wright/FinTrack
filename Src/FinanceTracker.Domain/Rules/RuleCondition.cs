using System.Globalization;
using System.Text.RegularExpressions;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// One test a <see cref="Rule"/> makes of a transaction its <see cref="Field"/> compared with <see cref="Value"/>
/// using <see cref="Operator"/>. Checked when made, so a stored condition can always be evaluated.
/// </summary>
public sealed record RuleCondition
{
    /// <summary>
    /// The longest a <see cref="RuleOperator.Regex"/> can take on one transaction before it's treated as not matching.
    /// </summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Digits and a <c>.</c> only, so <c>12,50</c> is refused rather than read as 1250. Amounts ignore their sign.
    /// </summary>
    private const NumberStyles AmountStyle = NumberStyles.AllowDecimalPoint;

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private RuleCondition()
    {
    }
#pragma warning restore CS8618

    /// <param name="field">What to look at.</param>
    /// <param name="operator">How to compare it.</param>
    /// <param name="value">What to compare it with. A number is written with a <c>.</c> for the decimal point, a type
    /// as <c>Debit</c> or <c>Credit</c> and an account as its id.</param>
    /// <param name="secondValue">The top of the range for <see cref="RuleOperator.Between"/>, null otherwise.</param>
    /// <param name="ignoreCase">Whether text comparisons ignore case. Has no effect on numbers, types or accounts.
    /// </param>
    /// <exception cref="ArgumentException">The operator doesn't suit the field, or a value doesn't suit either.
    /// </exception>
    public RuleCondition(RuleField field, RuleOperator @operator, string value, string? secondValue = null,
        bool ignoreCase = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Field = field;
        Operator = @operator;
        Value = value;
        SecondValue = secondValue;
        IgnoreCase = ignoreCase;

        Validate(field, @operator, value, secondValue, RegexOptions);
    }

    public RuleField Field { get; private init; }

    public RuleOperator Operator { get; private init; }

    public string Value { get; private init; }

    /// <summary>
    /// The top of the range for <see cref="RuleOperator.Between"/>, null otherwise.
    /// </summary>
    public string? SecondValue { get; private init; }

    /// <summary>
    /// Whether text comparisons ignore case, e.g. so <c>tesco</c> matches <c>TESCO STORES</c>.
    /// </summary>
    public bool IgnoreCase { get; private init; }

    /// <summary>
    /// Whether <paramref name="transaction"/> meets this condition. A field the bank didn't send never matches.
    /// </summary>
    public bool Matches(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        switch (Field)
        {
            case RuleField.Description:
                return MatchesText(transaction.Description);
            case RuleField.Category:
                return MatchesText(transaction.Category);
            case RuleField.ProviderCategory:
                return MatchesText(transaction.Metadata.ProviderCategory);
            case RuleField.ProviderClassification:
                return transaction.ProviderClassifications.Any(MatchesText);
            case RuleField.Counterparty:
                return MatchesText(transaction.Metadata.MerchantName) ||
                       MatchesText(transaction.Metadata.CounterPartyPreferredName);
            case RuleField.Amount:
                return MatchesAmount(Math.Abs(transaction.Amount));
            case RuleField.Type:
                return transaction.Type == ParseType(Value);
            case RuleField.Account:
                return transaction.AccountId == Guid.Parse(Value);
            default:
                throw new InvalidOperationException($"Unknown rule field {Field}.");
        }
    }

    private bool MatchesText(string? text)
    {
        if (text is null)
        {
            return false;
        }

        StringComparison comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        switch (Operator)
        {
            case RuleOperator.Equals:
                return string.Equals(text, Value, comparison);
            case RuleOperator.Contains:
                return text.Contains(Value, comparison);
            case RuleOperator.StartsWith:
                return text.StartsWith(Value, comparison);
            case RuleOperator.EndsWith:
                return text.EndsWith(Value, comparison);
            case RuleOperator.Regex:
                return MatchesRegex(text);
            default:
                throw new InvalidOperationException($"{Operator} doesn't compare text.");
        }
    }

    private bool MatchesRegex(string text)
    {
        try
        {
            return Regex.IsMatch(text, Value, RegexOptions, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private bool MatchesAmount(decimal amount)
    {
        decimal value = ParseAmount(Value);

        switch (Operator)
        {
            case RuleOperator.Equals:
                return amount == value;
            case RuleOperator.GreaterThan:
                return amount > value;
            case RuleOperator.LessThan:
                return amount < value;
            case RuleOperator.Between:
                return amount >= value && amount <= ParseAmount(SecondValue!);
            default:
                throw new InvalidOperationException($"{Operator} doesn't compare amounts.");
        }
    }

    private RegexOptions RegexOptions =>
        IgnoreCase ? RegexOptions.CultureInvariant | RegexOptions.IgnoreCase : RegexOptions.CultureInvariant;

    private static void Validate(RuleField field, RuleOperator @operator, string value, string? secondValue,
        RegexOptions regexOptions)
    {
        if (@operator != RuleOperator.Between && secondValue is not null)
        {
            throw new ArgumentException($"Only {RuleOperator.Between} takes a second value.", nameof(secondValue));
        }

        switch (field)
        {
            case RuleField.Description:
            case RuleField.Category:
            case RuleField.ProviderCategory:
            case RuleField.ProviderClassification:
            case RuleField.Counterparty:
                ValidateText(field, @operator, value, regexOptions);
                break;
            case RuleField.Amount:
                ValidateAmount(field, @operator, value, secondValue);
                break;
            case RuleField.Type:
                RequireOperator(field, @operator, RuleOperator.Equals);
                if (!TryParseType(value, out _))
                {
                    throw new ArgumentException($"'{value}' isn't a transaction type.", nameof(value));
                }

                break;
            case RuleField.Account:
                RequireOperator(field, @operator, RuleOperator.Equals);
                if (!Guid.TryParse(value, out _))
                {
                    throw new ArgumentException($"'{value}' isn't an account id.", nameof(value));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown rule field.");
        }
    }

    private static void ValidateText(RuleField field, RuleOperator @operator, string value, RegexOptions regexOptions)
    {
        RequireOperator(field, @operator, RuleOperator.Equals, RuleOperator.Contains, RuleOperator.StartsWith,
            RuleOperator.EndsWith, RuleOperator.Regex);

        if (@operator != RuleOperator.Regex)
        {
            return;
        }

        try
        {
            _ = new Regex(value, regexOptions, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"'{value}' isn't a valid regular expression: {ex.Message}", nameof(value),
                ex);
        }
    }

    private static void ValidateAmount(RuleField field, RuleOperator @operator, string value, string? secondValue)
    {
        RequireOperator(field, @operator, RuleOperator.Equals, RuleOperator.GreaterThan, RuleOperator.LessThan,
            RuleOperator.Between);

        if (!TryParseAmount(value, out decimal from))
        {
            throw new ArgumentException($"'{value}' isn't an amount.", nameof(value));
        }

        if (@operator != RuleOperator.Between)
        {
            return;
        }

        if (!TryParseAmount(secondValue, out decimal to))
        {
            throw new ArgumentException($"{RuleOperator.Between} needs a second amount.", nameof(secondValue));
        }

        if (to < from)
        {
            throw new ArgumentException($"{RuleOperator.Between} needs the second amount to be the larger.",
                nameof(secondValue));
        }
    }

    private static void RequireOperator(RuleField field, RuleOperator @operator, params RuleOperator[] allowed)
    {
        if (!allowed.Contains(@operator))
        {
            throw new ArgumentException($"{@operator} can't be used on {field}.", nameof(@operator));
        }
    }

    private static bool TryParseAmount(string? value, out decimal amount) =>
        decimal.TryParse(value, AmountStyle, CultureInfo.InvariantCulture, out amount);

    private static decimal ParseAmount(string value) => decimal.Parse(value, AmountStyle,
        CultureInfo.InvariantCulture);

    private static bool TryParseType(string value, out TransactionType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Enum.IsDefined(type) && !int.TryParse(value, out _);

    private static TransactionType ParseType(string value) => Enum.Parse<TransactionType>(value, ignoreCase: true);
}
