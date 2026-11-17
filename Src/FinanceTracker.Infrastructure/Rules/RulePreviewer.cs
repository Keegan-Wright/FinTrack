using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using DomainRule = FinanceTracker.Domain.Rules.Rule;

namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// Works out what applying a rule to existing transactions would change by applying it to copies that are never saved,
/// so the preview runs exactly what <see cref="IRuleApplicationService.ApplyToExistingAsync"/> would.
/// </summary>
internal static class RulePreviewer
{
    /// <summary>
    /// What applying <paramref name="draft"/> would change, without saving anything.
    /// </summary>
    /// <param name="rulesFor">The rules that reach each transaction, the draft among them.</param>
    /// <param name="draft">The rule being previewed, only the transactions it reaches are looked at.</param>
    /// <param name="transactions">The user's transactions, untracked, as they're changed in place.</param>
    /// <param name="names">The user's tags, bills and debts by id, to say what changes by name.</param>
    /// <param name="describe">What the user is shown of a transaction, see <see cref="Sharing.PrivateTransfers"/>.
    /// </param>
    /// <param name="now">The time now, in UTC.</param>
    public static RulePreview Preview(Func<BankTransaction, IReadOnlyList<DomainRule>> rulesFor, DomainRule draft,
        IEnumerable<BankTransaction> transactions, RulePreviewNames names, Func<BankTransaction, string> describe,
        DateTime now)
    {
        List<RulePreviewChange> changes = [];
        int leftAlone = 0;

        foreach (BankTransaction transaction in transactions.Where(draft.Reaches))
        {
            RuleOutcome outcome = RuleEvaluator.Evaluate(rulesFor(transaction), transaction);
            if (transaction.TagsSetByUser && !transaction.RuleTags.ToHashSet().SetEquals(outcome.Tags))
            {
                leftAlone++;
            }

            RulePreviewSnapshot before = RulePreviewSnapshot.Of(transaction);

            if (transaction.ApplyRules(outcome, now))
            {
                changes.Add(ToChange(transaction, before, names, describe(transaction)));
            }
        }

        return new RulePreview
        {
            Changed = changes.Count,
            LeftAlone = leftAlone,
            Changes = changes.OrderByDescending(static x => x.TransactionTime).ToList()
        };
    }

    /// <summary>
    /// What changed on a transaction, by name.
    /// </summary>
    /// <param name="transaction">The transaction, after the rules ran.</param>
    /// <param name="before">The transaction before.</param>
    /// <param name="names">The user's tags, bills and debts by id.</param>
    /// <param name="description">What the user is shown of it.</param>
    private static RulePreviewChange ToChange(BankTransaction transaction, RulePreviewSnapshot before,
        RulePreviewNames names, string description)
    {
        RulePreviewSnapshot after = RulePreviewSnapshot.Of(transaction);

        return new RulePreviewChange
        {
            TransactionId = transaction.Id,
            Description = description,
            Amount = transaction.Amount,
            TransactionTime = transaction.OccurredAt,
            TagsAdded = Names(after.Tags.Except(before.Tags), names.Tags),
            TagsRemoved = Names(before.Tags.Except(after.Tags), names.Tags),
            ClassificationsHidden = before.Visible.Except(after.Visible).Order().ToList(),
            ClassificationsShown = after.Visible.Except(before.Visible).Order().ToList(),
            TransferBefore = ToContract(before.Transfer),
            TransferAfter = ToContract(after.Transfer),
            BillBefore = NameOf(before.BillId, names.Bills),
            BillAfter = NameOf(after.BillId, names.Bills),
            DebtBefore = NameOf(before.DebtId, names.Debts),
            DebtAfter = NameOf(after.DebtId, names.Debts)
        };
    }

    /// <summary>
    /// A bill's or debt's name, null for none.
    /// </summary>
    /// <param name="id">Its id, null for none.</param>
    /// <param name="names">The names, by id.</param>
    private static string? NameOf(Guid? id, IReadOnlyDictionary<Guid, string> names)
    {
        if (id is not { } value)
        {
            return null;
        }

        return names.GetValueOrDefault(value);
    }

    /// <summary>
    /// The tags' names, in order, those not found left out.
    /// </summary>
    /// <param name="tagIds">The tags.</param>
    /// <param name="tagNames">The tags' names, by id.</param>
    private static List<string> Names(IEnumerable<Guid> tagIds, IReadOnlyDictionary<Guid, string> tagNames) =>
        tagIds.Select(tagNames.GetValueOrDefault).OfType<string>().Order().ToList();

    /// <summary>
    /// A transfer answer as the API sends it, null for none.
    /// </summary>
    /// <param name="decision">The answer, null for none.</param>
    private static TransferState? ToContract(TransferDecision? decision)
    {
        if (decision is not { } value)
        {
            return null;
        }

        return RuleMapper.MapEnum<TransferDecision, TransferState>(value);
    }
}
