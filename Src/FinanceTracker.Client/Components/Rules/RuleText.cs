using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Rules;

/// <summary>
/// How rules read on screen. Their fields and operators by name, and a line describing what each one does.
/// </summary>
public static class RuleText
{
    private static readonly RuleOperator[] TextOperators =
    [
        RuleOperator.Contains, RuleOperator.Equals, RuleOperator.StartsWith, RuleOperator.EndsWith, RuleOperator.Regex
    ];

    private static readonly RuleOperator[] AmountOperators =
        [RuleOperator.Equals, RuleOperator.GreaterThan, RuleOperator.LessThan, RuleOperator.Between];

    private static readonly RuleOperator[] EqualsOnly = [RuleOperator.Equals];

    /// <summary>
    /// The fields in the order they're offered, the ones people reach for first.
    /// </summary>
    public static IReadOnlyList<RuleField> Fields { get; } =
    [
        RuleField.Counterparty, RuleField.Description, RuleField.Amount, RuleField.Type, RuleField.Account,
        RuleField.Category, RuleField.ProviderClassification, RuleField.ProviderCategory
    ];

    public static string FieldName(RuleField field) => field switch
    {
        RuleField.Counterparty => "Merchant or payee",
        RuleField.Description => "Description",
        RuleField.Amount => "Amount",
        RuleField.Type => "Money in or out",
        RuleField.Account => "Account",
        RuleField.Category => "Category",
        RuleField.ProviderClassification => "Bank's classification",
        RuleField.ProviderCategory => "Bank's code",
        _ => field.ToString()
    };

    public static string OperatorName(RuleOperator @operator) => @operator switch
    {
        RuleOperator.Equals => "is",
        RuleOperator.Contains => "contains",
        RuleOperator.StartsWith => "starts with",
        RuleOperator.EndsWith => "ends with",
        RuleOperator.Regex => "matches pattern",
        RuleOperator.GreaterThan => "is over",
        RuleOperator.LessThan => "is under",
        RuleOperator.Between => "is between",
        _ => @operator.ToString()
    };

    public static string MatchModeName(RuleMatchMode matchMode) => matchMode == RuleMatchMode.Any ? "any" : "all";

    public static string ActionName(RuleActionType type) => type switch
    {
        RuleActionType.AddTag => "Add tag",
        RuleActionType.MarkAsTransfer => "Mark as a transfer",
        RuleActionType.MarkNotATransfer => "Mark as not a transfer",
        RuleActionType.HideClassification => "Hide bank classification",
        _ => type.ToString()
    };

    /// <summary>
    /// The operators that suit <paramref name="field"/>, as <c>RuleConditionValidator</c> allows, the usual one first.
    /// </summary>
    public static IReadOnlyList<RuleOperator> OperatorsFor(RuleField field)
    {
        if (IsText(field))
        {
            return TextOperators;
        }

        if (field == RuleField.Amount)
        {
            return AmountOperators;
        }

        return EqualsOnly;
    }

    /// <summary>
    /// Whether <paramref name="field"/> is compared as text, so case can matter.
    /// </summary>
    public static bool IsText(RuleField field) => field is RuleField.Description or RuleField.Category
        or RuleField.ProviderCategory or RuleField.ProviderClassification or RuleField.Counterparty;

    /// <summary>
    /// e.g. <c>Merchant or payee contains "tesco"</c> or <c>Amount is between 5.00 and 50.00</c>.
    /// </summary>
    /// <param name="accountNames">The user's accounts by id. An account not in it reads as removed.</param>
    public static string Describe(RuleCondition condition, IReadOnlyDictionary<Guid, string> accountNames)
    {
        string subject = $"{FieldName(condition.Field)} {OperatorName(condition.Operator)}";

        switch (condition.Field)
        {
            case RuleField.Amount when condition.Operator == RuleOperator.Between:
                return $"{subject} {Amount(condition.Value)} and {Amount(condition.SecondValue)}";
            case RuleField.Amount:
                return $"{subject} {Amount(condition.Value)}";
            case RuleField.Type:
                return $"{FieldName(condition.Field)} is {TypeName(condition.Value)}";
            case RuleField.Account:
                return $"{subject} {AccountName(condition.Value, accountNames)}";
            default:
                return $"{subject} \"{condition.Value}\"";
        }
    }

    /// <param name="tagNames">The user's tags by id. A tag not in it reads as deleted.</param>
    public static string Describe(RuleAction action, IReadOnlyDictionary<Guid, string> tagNames)
    {
        if (action.Type == RuleActionType.HideClassification)
        {
            return $"Hide bank classification {action.BankClassification}";
        }

        if (action.Type != RuleActionType.AddTag)
        {
            return ActionName(action.Type);
        }

        if (action.ClassificationId is { } tagId && tagNames.TryGetValue(tagId, out string? tagName))
        {
            return $"Add tag {tagName}";
        }

        return "Add a deleted tag";
    }

    /// <summary>
    /// One line for the list its conditions joined by and or, then what it does.
    /// </summary>
    public static string Summary(Rule rule, IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> tagNames)
    {
        string joiner = rule.MatchMode == RuleMatchMode.Any ? " or " : " and ";
        string conditions = string.Join(joiner, rule.Conditions.Select(x => Describe(x, accountNames)));

        if (rule.Actions.Count == 0)
        {
            return $"{conditions}, does nothing";
        }

        string actions = string.Join(", ", rule.Actions.Select(x => Describe(x, tagNames)));
        return $"{conditions} → {actions}";
    }

    /// <summary>
    /// Why <paramref name="rule"/> can't do its job as it stands, null when it can.
    /// </summary>
    public static string? Problem(Rule rule, IReadOnlyDictionary<Guid, string> accountNames)
    {
        if (rule.Actions.Count == 0)
        {
            return "Its tag was deleted, give it an action to turn it back on";
        }

        bool removedAccount = rule.Conditions.Any(x =>
            x.Field == RuleField.Account &&
            (!Guid.TryParse(x.Value, out Guid accountId) || !accountNames.ContainsKey(accountId)));

        if (removedAccount)
        {
            return "Looks at an account you no longer have, so it can't match";
        }

        return null;
    }

    /// <summary>
    /// What applying a rule would do to one transaction, e.g. <c>+ Shopping</c>, <c>− Food</c> or
    /// <c>Marked as a transfer</c>.
    /// </summary>
    public static IEnumerable<string> Changes(RulePreviewChange change)
    {
        foreach (string tag in change.TagsAdded)
        {
            yield return $"+ {tag}";
        }

        foreach (string tag in change.TagsRemoved)
        {
            yield return $"− {tag}";
        }

        foreach (string classification in change.ClassificationsHidden)
        {
            yield return $"Hides {classification}";
        }

        foreach (string classification in change.ClassificationsShown)
        {
            yield return $"Shows {classification}";
        }

        if (change.TransferAfter != change.TransferBefore)
        {
            yield return TransferChange(change.TransferAfter);
        }
    }

    /// <summary>
    /// The rule that added <paramref name="tag"/> to <paramref name="transaction"/>, null when the user added it or it
    /// came from the bank.
    /// </summary>
    public static string? TaggedBy(Transaction transaction, string tag) =>
        transaction.TagsFromRules.FirstOrDefault(x => x.Tag == tag)?.RuleName;

    /// <summary>
    /// e.g. <c>Shopping from your rule Groceries</c>, one line per tag a rule added.
    /// </summary>
    public static IEnumerable<string> TaggedByLines(Transaction transaction) =>
        transaction.TagsFromRules.Select(static x => $"{x.Tag} from your rule {x.RuleName}");

    public static string TypeName(string value) =>
        string.Equals(value, "Credit", StringComparison.OrdinalIgnoreCase) ? "money in" : "money out";

    private static string TransferChange(TransferState? after) => after switch
    {
        TransferState.Transfer => "Marked as a transfer",
        TransferState.NotATransfer => "Counts as income or spending",
        TransferState.NeedsReview => "Transfer to review",
        _ => "Transfer answer taken back"
    };

    private static string Amount(string? value)
    {
        if (decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
        {
            return CurrencyFormatter.Format(amount);
        }

        return value ?? string.Empty;
    }

    private static string AccountName(string value, IReadOnlyDictionary<Guid, string> accountNames)
    {
        if (Guid.TryParse(value, out Guid accountId) && accountNames.TryGetValue(accountId, out string? name))
        {
            return name;
        }

        return "a removed account";
    }
}
