using FinanceTracker.Contracts.Rules;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
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

    /// <summary>
    /// A blank bank classification, as a form sends it.
    /// </summary>
    public static DomainRuleAction ToDomain(RuleAction action) =>
        new(MapEnum<RuleActionType, DomainRuleActionType>(action.Type), action.ClassificationId,
            string.IsNullOrWhiteSpace(action.BankClassification) ? null : action.BankClassification.Trim());

    /// <summary>
    /// A rule to start from that matches <paramref name="transaction"/> its merchant, or the description when the
    /// bank sent none, adding the user's tags on it, hiding the bank classifications they hid and giving their transfer
    /// answer. Without any of those it adds a tag the user still has to choose.
    /// </summary>
    /// <param name="tagIds">The user's tags, so only theirs are added.</param>
    public static SaveRule DraftFrom(BankTransaction transaction, IReadOnlySet<Guid> tagIds)
    {
        string? merchant = FirstNonBlank(transaction.Metadata.MerchantName,
            transaction.Metadata.CounterPartyPreferredName);

        RuleCondition condition = new() { Field = RuleField.Counterparty, Operator = RuleOperator.Contains };
        if (merchant is null)
        {
            condition.Field = RuleField.Description;
            condition.Value = transaction.Description;
        }
        else
        {
            condition.Value = merchant;
        }

        List<RuleAction> actions = transaction.CustomClassificationIds
            .Where(tagIds.Contains)
            .Select(static x => new RuleAction { Type = RuleActionType.AddTag, ClassificationId = x })
            .ToList();

        actions.AddRange(transaction.HiddenClassifications
            .Where(static x => x.HiddenByUser)
            .Select(static x => new RuleAction
            {
                Type = RuleActionType.HideClassification,
                BankClassification = x.Classification
            }));

        if (UsersTransferAction(transaction.Transfer) is { } transferAction)
        {
            actions.Add(new RuleAction { Type = transferAction });
        }

        if (actions.Count == 0)
        {
            actions.Add(new RuleAction { Type = RuleActionType.AddTag });
        }

        return new SaveRule
        {
            Name = merchant ?? transaction.Description,
            Conditions = [condition],
            Actions = actions
        };
    }

    public static TTo MapEnum<TFrom, TTo>(TFrom value) where TFrom : struct, Enum where TTo : struct, Enum =>
        Enum.Parse<TTo>(value.ToString());

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x))?.Trim();

    /// <summary>
    /// The user's own answer as an action, null when they haven't decided or it's for review.
    /// </summary>
    private static RuleActionType? UsersTransferAction(TransferInfo? transfer)
    {
        if (transfer is not { Source: DecisionSource.User })
        {
            return null;
        }

        return transfer.Decision switch
        {
            TransferDecision.Transfer => RuleActionType.MarkAsTransfer,
            TransferDecision.NotATransfer => RuleActionType.MarkNotATransfer,
            _ => null
        };
    }

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
        ClassificationId = action.ClassificationId,
        BankClassification = action.BankClassification
    };
}
