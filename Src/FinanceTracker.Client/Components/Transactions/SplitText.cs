using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// What's said about splitting a transaction, and the sums the split dialog checks.
/// </summary>
public static class SplitText
{
    /// <summary>
    /// The menu and details action: <c>Split</c>, <c>Edit split</c> once it's split, or why it can't be split yet.
    /// </summary>
    public static string ActionLabel(Transaction transaction)
    {
        if (transaction.Pending)
        {
            return "Split once it's settled";
        }

        if (transaction.Splits.Count > 0)
        {
            return "Edit split";
        }

        return "Split";
    }

    /// <summary>
    /// The parts to start the dialog with the split as it is, or two empty parts.
    /// </summary>
    public static List<SplitPartDraft> Drafts(Transaction transaction)
    {
        if (transaction.Splits.Count == 0)
        {
            return [new SplitPartDraft(), new SplitPartDraft()];
        }

        return transaction.Splits.Select(static x => new SplitPartDraft
        {
            Amount = Math.Abs(x.Amount), TagId = x.TagId ?? Guid.Empty, Note = x.Note
        }).ToList();
    }

    /// <summary>
    /// How much of the transaction the parts haven't covered yet, below nothing when they come to more.
    /// </summary>
    public static decimal Remaining(Transaction transaction, IEnumerable<SplitPartDraft> parts) =>
        Math.Abs(transaction.Amount) - parts.Sum(static x => x.Amount);

    /// <summary>
    /// e.g. <c>£10.00 left to split</c>.
    /// </summary>
    public static string RemainingText(decimal remaining)
    {
        if (remaining > 0)
        {
            return $"{CurrencyFormatter.Format(remaining)} left to split";
        }

        if (remaining < 0)
        {
            return $"{CurrencyFormatter.Format(-remaining)} more than the transaction";
        }

        return "The parts add up";
    }

    /// <summary>
    /// Whether the parts can be saved: two to <see cref="SplitTransaction.MaxParts"/> of them, none empty, adding up
    /// to the transaction with short notes.
    /// </summary>
    public static bool CanSave(Transaction transaction, IReadOnlyList<SplitPartDraft> parts)
    {
        if (parts.Count is < 2 or > SplitTransaction.MaxParts)
        {
            return false;
        }

        if (parts.Any(static x => x.Amount <= 0 || x.Note?.Length > SplitTransaction.MaxNoteLength))
        {
            return false;
        }

        return Remaining(transaction, parts) == 0;
    }

    /// <summary>
    /// The parts as the API takes them, signed like the transaction.
    /// </summary>
    public static SplitTransaction Request(Transaction transaction, IEnumerable<SplitPartDraft> parts)
    {
        int sign = Math.Sign(transaction.Amount);
        return new SplitTransaction
        {
            Parts = parts.Select(x => new SplitTransactionPart
            {
                Amount = sign * x.Amount,
                TagId = x.TagId == Guid.Empty ? null : x.TagId,
                Note = x.Note
            }).ToList()
        };
    }

    /// <summary>
    /// e.g. <c>£60.00 Groceries</c>, or <c>£10.00 untagged</c>.
    /// </summary>
    /// <summary>
    /// The tags a part can have: the user's own, and those already on <paramref name="transaction"/>'s parts, which on
    /// a shared one may be the other person's.
    /// </summary>
    public static List<Classification> TagChoices(IReadOnlyList<Classification> own, Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(transaction);

        List<Classification> choices = own.ToList();
        foreach (TransactionSplitPart part in transaction.Splits)
        {
            if (part is not { TagId: { } tagId, Tag: { } tag } || choices.Exists(x => x.ClassificationId == tagId))
            {
                continue;
            }

            choices.Add(new Classification { ClassificationId = tagId, Tag = tag });
        }

        return choices;
    }

    public static string PartText(TransactionSplitPart part) =>
        $"{CurrencyFormatter.Format(Math.Abs(part.Amount))} {part.Tag ?? "untagged"}";

    /// <summary>
    /// The split chip's tooltip, e.g. <c>Split into £60.00 Groceries, £40.00 untagged</c>.
    /// </summary>
    public static string Summary(Transaction transaction) =>
        $"Split into {string.Join(", ", transaction.Splits.Select(PartText))}";
}
