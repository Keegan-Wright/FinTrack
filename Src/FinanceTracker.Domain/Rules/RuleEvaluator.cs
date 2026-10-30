using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// Runs a user's rules over a transaction. Enabled rules run in <see cref="Rule.Order"/>, each matching rule's
/// actions stacking
/// <list type="bullet">
/// <item>Tags from every matching rule are added, each credited to the first rule that added it.</item>
/// <item>The transfer answer has one value, so the first matching rule to give one wins.</item>
/// <item>A matching rule with <see cref="Rule.StopProcessing"/> ends the run.</item>
/// </list>
/// </summary>
/// <remarks>
/// Running again gives the same result, and what a rule no longer does is undone, see
/// <see cref="BankTransaction.ApplyRules"/>. The user's own decisions are never overridden.
/// </remarks>
public static class RuleEvaluator
{
    /// <summary>
    /// What <paramref name="rules"/> would do to <paramref name="transaction"/>, without changing it.
    /// </summary>
    /// <exception cref="InvalidOperationException">A rule belongs to another user.</exception>
    public static RuleOutcome Evaluate(IEnumerable<Rule> rules, BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(transaction);

        List<RuleTag> tags = [];
        List<HiddenClassification> hidden = [];
        RuleTransfer? transfer = null;

        // By id after order so rules sharing a position always run the same way
        IEnumerable<Rule> enabled = rules
            .Where(static x => x.IsEnabled)
            .OrderBy(static x => x.Order)
            .ThenBy(static x => x.Id);

        foreach (Rule rule in enabled)
        {
            if (rule.UserId != transaction.UserId)
            {
                throw new InvalidOperationException(
                    $"Rule {rule.Id} belongs to another user than transaction {transaction.Id}.");
            }

            if (!rule.Matches(transaction))
            {
                continue;
            }

            foreach (RuleAction action in rule.Actions)
            {
                switch (action.Type)
                {
                    case RuleActionType.AddTag:
                        AddTag(tags, action.ClassificationId!.Value, rule.Id);
                        break;
                    case RuleActionType.HideClassification:
                        Hide(hidden, action.BankClassification!, rule.Id);
                        break;
                    default:
                        transfer ??= new RuleTransfer(ToDecision(action.Type), rule.Id);
                        break;
                }
            }

            if (rule.StopProcessing)
            {
                break;
            }
        }

        return new RuleOutcome(tags, transfer, hidden);
    }

    /// <summary>
    /// Runs <paramref name="rules"/> over <paramref name="transaction"/> and records the outcome on it.
    /// </summary>
    /// <returns>Whether the transaction changed.</returns>
    /// <exception cref="InvalidOperationException">A rule belongs to another user.</exception>
    public static bool Apply(IEnumerable<Rule> rules, BankTransaction transaction, DateTime decidedAt)
    {
        RuleOutcome outcome = Evaluate(rules, transaction);
        return transaction.ApplyRules(outcome, decidedAt);
    }

    private static void AddTag(List<RuleTag> tags, Guid classificationId, Guid ruleId)
    {
        if (tags.Any(x => x.ClassificationId == classificationId))
        {
            return;
        }

        tags.Add(new RuleTag(classificationId, ruleId));
    }

    private static void Hide(List<HiddenClassification> hidden, string classification, Guid ruleId)
    {
        if (hidden.Exists(x => string.Equals(x.Classification, classification, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        hidden.Add(new HiddenClassification(classification, ruleId));
    }

    private static TransferDecision ToDecision(RuleActionType type)
    {
        if (type == RuleActionType.MarkAsTransfer)
        {
            return TransferDecision.Transfer;
        }

        return TransferDecision.NotATransfer;
    }
}
