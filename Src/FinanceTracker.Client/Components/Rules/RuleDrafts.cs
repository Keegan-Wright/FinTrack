using FinanceTracker.Contracts.Rules;

namespace FinanceTracker.Client.Components.Rules;

/// <summary>
/// The <see cref="SaveRule"/> a rule is edited as, copied so changes on screen don't reach the rule shown elsewhere.
/// </summary>
public static class RuleDrafts
{
    /// <summary>
    /// A rule to start from one condition on the merchant and one tag to add.
    /// </summary>
    public static SaveRule New() => new()
    {
        Conditions = [NewCondition(RuleField.Counterparty)],
        Actions = [new RuleAction { Type = RuleActionType.AddTag }]
    };

    public static SaveRule From(Rule rule) => new()
    {
        Name = rule.Name,
        IsEnabled = rule.IsEnabled,
        MatchMode = rule.MatchMode,
        StopProcessing = rule.StopProcessing,
        Conditions = rule.Conditions.Select(Copy).ToList(),
        Actions = rule.Actions
            .Select(static x => new RuleAction { Type = x.Type, ClassificationId = x.ClassificationId })
            .ToList()
    };

    /// <summary>
    /// <paramref name="rule"/> on or off with everything else as it is, for the switch on the list.
    /// </summary>
    public static SaveRule WithEnabled(Rule rule, bool isEnabled)
    {
        SaveRule draft = From(rule);
        draft.IsEnabled = isEnabled;
        return draft;
    }

    /// <summary>
    /// A blank condition on <paramref name="field"/> with its usual operator, money out for a type.
    /// </summary>
    public static RuleCondition NewCondition(RuleField field) => new()
    {
        Field = field,
        Operator = RuleText.OperatorsFor(field)[0],
        Value = field == RuleField.Type ? "Debit" : string.Empty
    };

    private static RuleCondition Copy(RuleCondition condition) => new()
    {
        Field = condition.Field,
        Operator = condition.Operator,
        Value = condition.Value,
        SecondValue = condition.SecondValue,
        IgnoreCase = condition.IgnoreCase
    };
}
