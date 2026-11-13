using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// Runs the rules that reach a transaction over it: its owner's, and those of anyone they let change its account.
/// The owner's run first, then each other person's, by person so it's always the same way. Each person's enabled rules
/// run in their <see cref="Rule.Order"/>, each matching rule's actions stacking
/// <list type="bullet">
/// <item>Tags from every matching rule are added, each credited to the first rule that added it.</item>
/// <item>The transfer answer has one value, so the first matching rule to give one wins. The same for the bill it
/// pays.</item>
/// <item>A matching rule with <see cref="Rule.StopProcessing"/> ends its owner's run. Another person's rules still
/// run, it isn't theirs to stop.</item>
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
    /// <param name="rules">The rules that reach it, which the caller works out: the owner's, and those of anyone
    /// allowed to change its account.</param>
    public static RuleOutcome Evaluate(IEnumerable<Rule> rules, BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(transaction);

        List<RuleTag> tags = [];
        List<HiddenClassification> hidden = [];
        RuleTransfer? transfer = null;
        RuleBill? bill = null;
        RuleDebt? debt = null;
        List<Guid> matched = [];

        // The owner's first, then each other person's. By id after order so rules sharing a position always run the
        // same way
        Guid ownerId = transaction.UserId;
        IEnumerable<IGrouping<Guid, Rule>> byPerson = rules
            .Where(static x => x.IsEnabled)
            .GroupBy(static x => x.UserId)
            .OrderBy(x => x.Key != ownerId)
            .ThenBy(static x => x.Key);

        foreach (IGrouping<Guid, Rule> person in byPerson)
        {
            foreach (Rule rule in person.OrderBy(static x => x.Order).ThenBy(static x => x.Id))
            {
                if (!rule.Matches(transaction))
                {
                    continue;
                }

                matched.Add(rule.Id);
                Collect(rule, tags, hidden, ref transfer, ref bill, ref debt);

                if (rule.StopProcessing)
                {
                    break;
                }
            }
        }

        return new RuleOutcome(tags, transfer, hidden, bill, debt) { MatchedRuleIds = matched };
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

    /// <summary>
    /// Adds what a matching <paramref name="rule"/> does to what's been collected so far.
    /// </summary>
    private static void Collect(Rule rule, List<RuleTag> tags, List<HiddenClassification> hidden,
        ref RuleTransfer? transfer, ref RuleBill? bill, ref RuleDebt? debt)
    {
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
                case RuleActionType.PayBill:
                    bill ??= new RuleBill(action.BillId!.Value, rule.Id);
                    break;
                case RuleActionType.PayDebt:
                    debt ??= new RuleDebt(action.DebtId!.Value, rule.Id);
                    break;
                default:
                    transfer ??= new RuleTransfer(ToDecision(action.Type), rule.Id);
                    break;
            }
        }
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
