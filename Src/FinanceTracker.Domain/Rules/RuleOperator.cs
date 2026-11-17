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

    /// <summary>
    /// Text containing the value.
    /// </summary>
    Contains = 2,

    /// <summary>
    /// Text starting with the value.
    /// </summary>
    StartsWith = 3,

    /// <summary>
    /// Text ending with the value.
    /// </summary>
    EndsWith = 4,

    /// <summary>
    /// Text matching the value as a regular expression, see <see cref="RuleCondition.RegexTimeout"/>.
    /// </summary>
    Regex = 5,

    /// <summary>
    /// A number more than the value.
    /// </summary>
    GreaterThan = 6,

    /// <summary>
    /// A number less than the value.
    /// </summary>
    LessThan = 7,

    /// <summary>
    /// A number from the value to the second value, both included.
    /// </summary>
    Between = 8
}
