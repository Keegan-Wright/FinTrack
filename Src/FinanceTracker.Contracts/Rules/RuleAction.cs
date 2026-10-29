using System.ComponentModel;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// Something a rule does to each transaction it matches.
/// </summary>
public class RuleAction
{
    /// <summary>
    /// What it does.
    /// </summary>
    [Description("What the action does")]
    public RuleActionType Type { get; set; }

    /// <summary>
    /// The tag <see cref="RuleActionType.AddTag"/> adds, null for the other types.
    /// </summary>
    [Description("The tag to add, for add tag only")]
    public Guid? ClassificationId { get; set; }
}
