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

    /// <summary>
    /// What it looks at.
    /// </summary>
    public RuleField Field { get; private init; }

    /// <summary>
    /// How it compares.
    /// </summary>
    public RuleOperator Operator { get; private init; }

    /// <summary>
    /// What it compares with.
    /// </summary>
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
    /// <param name="transaction">The transaction to check.</param>
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

    /// <summary>
    /// Whether <paramref name="text"/> meets the condition. Null never does.
    /// </summary>
    /// <param name="text">The transaction's text for the field.</param>
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

    /// <summary>
    /// Whether <paramref name="text"/> matches the regular expression. Taking longer than
    /// <see cref="RegexTimeout"/> counts as not matching.
    /// </summary>
    /// <param name="text">The transaction's text for the field.</param>
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

    /// <summary>
    /// Whether <paramref name="amount"/> meets the condition.
    /// </summary>
    /// <param name="amount">The transaction's amount, ignoring its sign.</param>
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

    /// <summary>
    /// The options for the regular expression, ignoring case when the condition does.
    /// </summary>
    private RegexOptions RegexOptions =>
        IgnoreCase ? RegexOptions.CultureInvariant | RegexOptions.IgnoreCase : RegexOptions.CultureInvariant;

    /// <summary>
    /// Checks the operator suits the field and the values suit both.
    /// </summary>
    /// <param name="field">What to look at.</param>
    /// <param name="operator">How to compare it.</param>
    /// <param name="value">What to compare it with.</param>
    /// <param name="secondValue">The top of the range for <see cref="RuleOperator.Between"/>, null otherwise.</param>
    /// <param name="regexOptions">The options a regular expression would run with.</param>
    /// <exception cref="ArgumentException">Something doesn't suit.</exception>
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

    /// <summary>
    /// Checks a text field's operator, and that a regular expression is valid.
    /// </summary>
    /// <param name="field">What to look at.</param>
    /// <param name="operator">How to compare it.</param>
    /// <param name="value">What to compare it with.</param>
    /// <param name="regexOptions">The options a regular expression would run with.</param>
    /// <exception cref="ArgumentException">The operator doesn't suit text, or the regular expression is invalid.
    /// </exception>
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

    /// <summary>
    /// Checks an amount's operator, and that the values are amounts.
    /// </summary>
    /// <param name="field">What to look at.</param>
    /// <param name="operator">How to compare it.</param>
    /// <param name="value">What to compare it with.</param>
    /// <param name="secondValue">The top of the range for <see cref="RuleOperator.Between"/>, null otherwise.</param>
    /// <exception cref="ArgumentException">The operator doesn't suit an amount, or a value isn't an amount.
    /// </exception>
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

    /// <summary>
    /// Checks <paramref name="operator"/> is one of <paramref name="allowed"/>.
    /// </summary>
    /// <param name="field">The field, for the message.</param>
    /// <param name="operator">The operator to check.</param>
    /// <param name="allowed">The operators the field takes.</param>
    /// <exception cref="ArgumentException">It isn't one of them.</exception>
    private static void RequireOperator(RuleField field, RuleOperator @operator, params RuleOperator[] allowed)
    {
        if (!allowed.Contains(@operator))
        {
            throw new ArgumentException($"{@operator} can't be used on {field}.", nameof(@operator));
        }
    }

    /// <summary>
    /// Reads an amount written with a <c>.</c> for the decimal point.
    /// </summary>
    /// <param name="value">The amount as written.</param>
    /// <param name="amount">The amount read, 0 when it isn't one.</param>
    private static bool TryParseAmount(string? value, out decimal amount) =>
        decimal.TryParse(value, AmountStyle, CultureInfo.InvariantCulture, out amount);

    /// <summary>
    /// Reads an amount already checked by <see cref="TryParseAmount"/>.
    /// </summary>
    /// <param name="value">The amount as written.</param>
    private static decimal ParseAmount(string value) => decimal.Parse(value, AmountStyle,
        CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a transaction type by name, in any case. A number isn't taken.
    /// </summary>
    /// <param name="value">The type as written.</param>
    /// <param name="type">The type read.</param>
    private static bool TryParseType(string value, out TransactionType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Enum.IsDefined(type) && !int.TryParse(value, out _);

    /// <summary>
    /// Reads a transaction type already checked by <see cref="TryParseType"/>.
    /// </summary>
    /// <param name="value">The type as written.</param>
    private static TransactionType ParseType(string value) => Enum.Parse<TransactionType>(value, ignoreCase: true);
}
