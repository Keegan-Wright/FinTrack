using System.ComponentModel;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// One of the user defined rules.
/// </summary>
public class Rule
{
    /// <summary>
    /// Unique identifier for the rule.
    /// </summary>
    [Description("Unique identifier for the rule")]
    public Guid Id { get; init; }

    /// <summary>
    /// The rule's name.
    /// </summary>
    [Description("The rule's name")]
    public required string Name { get; init; }

    /// <summary>
    /// Where it runs among the user's rules, lowest first.
    /// </summary>
    [Description("Where the rule runs among the user's rules, lowest first")]
    public int Order { get; init; }

    /// <summary>
    /// Whether the rule runs. One left with no actions, when the only tag it added was deleted, is disabled until
    /// it's given one.
    /// </summary>
    [Description("Whether the rule runs")]
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether a transaction has to meet all or any of the conditions.
    /// </summary>
    [Description("Whether a transaction has to meet all or any of the conditions")]
    public RuleMatchMode MatchMode { get; init; }

    /// <summary>
    /// Whether rules after this one are skipped for a transaction it matches.
    /// </summary>
    [Description("Whether later rules are skipped for a transaction this one matches")]
    public bool StopProcessing { get; init; }

    /// <summary>
    /// What the rule tests.
    /// </summary>
    [Description("What the rule tests")]
    public List<RuleCondition> Conditions { get; init; } = [];

    /// <summary>
    /// What the rule does to a matching transaction.
    /// </summary>
    [Description("What the rule does to a matching transaction")]
    public List<RuleAction> Actions { get; init; } = [];
}
