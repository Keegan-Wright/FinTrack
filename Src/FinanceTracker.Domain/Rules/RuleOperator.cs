namespace FinanceTracker.Domain.Rules;

/// <summary>
/// How a <see cref="RuleCondition"/> compares its field with its value. Which suit a field is checked when the
/// condition is made.
/// </summary>
public enum RuleOperator
{
    /// <summary>
    /// Text equal to the value, a number equal to it, or the type or account it names.
    /// </summary>
    Equals = 1,

    Contains = 2,

    StartsWith = 3,

    EndsWith = 4,

    /// <summary>
    /// Text matching the value as a regular expression, see <see cref="RuleCondition.RegexTimeout"/>.
    /// </summary>
    Regex = 5,

    GreaterThan = 6,

    LessThan = 7,

    /// <summary>
    /// A number from the value to the second value, both included.
    /// </summary>
    Between = 8
}
