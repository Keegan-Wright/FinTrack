using System.ComponentModel;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// One of the user's rules, or one another person shares with them.
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

    /// <summary>
    /// The first and last name of whoever shares it with the user, null when it's their own.
    /// </summary>
    [Description("Who shares the rule with the user, empty when it's their own")]
    public string? SharedBy { get; init; }

    /// <summary>
    /// Whether the user can edit it and switch it on or off. Their own, or shared with them with changes allowed.
    /// Only its owner deletes it, moves it or applies it to past transactions.
    /// </summary>
    [Description("Whether the user can edit the rule")]
    public bool CanChange { get; init; } = true;

    /// <summary>
    /// For a rule someone else shares, the names of the accounts, tags, bills and debts it uses that aren't the
    /// user's, by id. An account the user can't see is named as private.
    /// </summary>
    [Description("Names of the accounts, tags, bills and debts a shared rule uses, by id")]
    public Dictionary<Guid, string> Names { get; init; } = [];

    /// <summary>
    /// For a rule someone else shares with changes allowed, the tags its actions can add. Its owner's tags shared with
    /// the user, and those already on it, by id.
    /// </summary>
    [Description("The tags a shared rule the user can edit can add, by id")]
    public Dictionary<Guid, string> TagChoices { get; init; } = [];

    /// <summary>
    /// For a rule someone else shares with changes allowed, the bills its actions can pay: its owner's bills shared
    /// with the user, and those already on it, by id.
    /// </summary>
    [Description("The bills a shared rule the user can edit can pay, by id")]
    public Dictionary<Guid, string> BillChoices { get; init; } = [];

    /// <summary>
    /// For a rule someone else shares with changes allowed, the debts its actions can pay off. Its owner's debts shared
    /// with the user, and those already on it, by id.
    /// </summary>
    [Description("The debts a shared rule the user can edit can pay off, by id")]
    public Dictionary<Guid, string> DebtChoices { get; init; } = [];
}
