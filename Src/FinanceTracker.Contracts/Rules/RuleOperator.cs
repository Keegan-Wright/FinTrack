using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// How a <see cref="RuleCondition"/> compares its field with its value. Text fields take the first five values, amounts
/// <see cref="Equals"/> and the last three, type and account only <see cref="Equals"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RuleOperator>))]
public enum RuleOperator
{
    /// <summary>
    /// The whole value, for text, amounts, types and accounts.
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
    /// A regex treated as not matching if it takes too long on a transaction.
    /// </summary>
    Regex = 5,

    /// <summary>
    /// An amount over the value.
    /// </summary>
    GreaterThan = 6,

    /// <summary>
    /// An amount under the value.
    /// </summary>
    LessThan = 7,

    /// <summary>
    /// From the value to the second value, both included.
    /// </summary>
    Between = 8
}
