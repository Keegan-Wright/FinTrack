using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// How many of a rule's conditions a transaction has to meet.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RuleMatchMode>))]
public enum RuleMatchMode
{
    /// <summary>
    /// Every condition.
    /// </summary>
    All = 1,

    /// <summary>
    /// At least one condition.
    /// </summary>
    Any = 2
}
