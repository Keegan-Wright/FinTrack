using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// What a <see cref="RuleAction"/> does to a matching transaction.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RuleActionType>))]
public enum RuleActionType
{
    /// <summary>
    /// Adds a tag. Tags from every matching rule are added.
    /// </summary>
    AddTag = 1,

    /// <summary>
    /// Decides it's money moved between the user's own accounts. The first matching rule to decide wins.
    /// </summary>
    MarkAsTransfer = 2,

    /// <summary>
    /// Decides it counts as income or spending. The first matching rule to decide wins.
    /// </summary>
    MarkNotATransfer = 3,

    /// <summary>
    /// Hides one of the bank's classifications, every matching rule's are hidden.
    /// </summary>
    HideClassification = 4
}
