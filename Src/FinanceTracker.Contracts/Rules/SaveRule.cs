using System.ComponentModel;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// A new rule, or everything about an existing one.
/// </summary>
public class SaveRule
{
    /// <summary>
    /// The rule's name.
    /// </summary>
    [Description("The rule's name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the rule runs.
    /// </summary>
    [Description("Whether the rule runs")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether a transaction has to meet all or any of the conditions.
    /// </summary>
    [Description("Whether a transaction has to meet all or any of the conditions")]
    public RuleMatchMode MatchMode { get; set; } = RuleMatchMode.All;

    /// <summary>
    /// Whether rules after this one are skipped for a transaction it matches.
    /// </summary>
    [Description("Whether later rules are skipped for a transaction this one matches")]
    public bool StopProcessing { get; set; }

    /// <summary>
    /// At least one.
    /// </summary>
    [Description("What the rule tests, at least one")]
    public List<RuleCondition> Conditions { get; set; } = [];

    /// <summary>
    /// At least one, each tag once and at most one that decides the transfer answer.
    /// </summary>
    [Description("What the rule does to a matching transaction, at least one")]
    public List<RuleAction> Actions { get; set; } = [];
}
