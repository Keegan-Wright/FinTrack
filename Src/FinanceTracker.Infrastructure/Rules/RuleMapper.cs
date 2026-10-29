using FinanceTracker.Contracts.Rules;
using DomainRule = FinanceTracker.Domain.Rules.Rule;
using DomainRuleAction = FinanceTracker.Domain.Rules.RuleAction;
using DomainRuleActionType = FinanceTracker.Domain.Rules.RuleActionType;
using DomainRuleCondition = FinanceTracker.Domain.Rules.RuleCondition;
using DomainRuleField = FinanceTracker.Domain.Rules.RuleField;
using DomainRuleMatchMode = FinanceTracker.Domain.Rules.RuleMatchMode;
using DomainRuleOperator = FinanceTracker.Domain.Rules.RuleOperator;

namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// Between the domain's rules and their contracts.
/// </summary>
internal static class RuleMapper
{
    public static Rule ToContract(DomainRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Order = rule.Order,
        IsEnabled = rule.IsEnabled,
        MatchMode = MapEnum<DomainRuleMatchMode, RuleMatchMode>(rule.MatchMode),
        StopProcessing = rule.StopProcessing,
        Conditions = rule.Conditions.Select(ToContract).ToList(),
        Actions = rule.Actions.Select(ToContract).ToList()
    };

    public static DomainRuleMatchMode ToDomain(RuleMatchMode matchMode) =>
        MapEnum<RuleMatchMode, DomainRuleMatchMode>(matchMode);

    /// <summary>
    /// A blank second value, as a form sends it, is no second value.
    /// </summary>
    public static DomainRuleCondition ToDomain(RuleCondition condition)
    {
        string? secondValue = string.IsNullOrWhiteSpace(condition.SecondValue) ? null : condition.SecondValue;

        return new DomainRuleCondition(MapEnum<RuleField, DomainRuleField>(condition.Field),
            MapEnum<RuleOperator, DomainRuleOperator>(condition.Operator), condition.Value, secondValue,
            condition.IgnoreCase);
    }

    public static DomainRuleAction ToDomain(RuleAction action) =>
        new(MapEnum<RuleActionType, DomainRuleActionType>(action.Type), action.ClassificationId);

    public static TTo MapEnum<TFrom, TTo>(TFrom value) where TFrom : struct, Enum where TTo : struct, Enum =>
        Enum.Parse<TTo>(value.ToString());

    private static RuleCondition ToContract(DomainRuleCondition condition) => new()
    {
        Field = MapEnum<DomainRuleField, RuleField>(condition.Field),
        Operator = MapEnum<DomainRuleOperator, RuleOperator>(condition.Operator),
        Value = condition.Value,
        SecondValue = condition.SecondValue,
        IgnoreCase = condition.IgnoreCase
    };

    private static RuleAction ToContract(DomainRuleAction action) => new()
    {
        Type = MapEnum<DomainRuleActionType, RuleActionType>(action.Type),
        ClassificationId = action.ClassificationId
    };
}
