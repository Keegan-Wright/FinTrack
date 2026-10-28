using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Review;

/// <summary>
/// Groups the transactions waiting for review so a suggested pair shows once, both halves side by side.
/// </summary>
public static class ReviewPairs
{
    /// <param name="toReview">Newest first, as the review list comes.</param>
    /// <returns>In the same order, each pair where its first half was.</returns>
    public static List<ReviewPair> Group(IReadOnlyList<Transaction> toReview)
    {
        Dictionary<Guid, Transaction> byId = toReview.ToDictionary(static x => x.TransactionId);
        HashSet<Guid> shown = [];
        List<ReviewPair> pairs = [];

        foreach (Transaction transaction in toReview)
        {
            if (!shown.Add(transaction.TransactionId))
            {
                continue;
            }

            Transaction? partner = WaitingPartner(transaction, byId, shown);
            if (partner is null)
            {
                pairs.Add(new ReviewPair(transaction, null));
                continue;
            }

            shown.Add(partner.TransactionId);
            pairs.Add(transaction.Amount < 0
                ? new ReviewPair(transaction, partner)
                : new ReviewPair(partner, transaction));
        }

        return pairs;
    }

    /// <summary>
    /// The other half of <paramref name="transaction"/>'s suggested pair when it's waiting too and not shown yet.
    /// </summary>
    private static Transaction? WaitingPartner(Transaction transaction, Dictionary<Guid, Transaction> byId,
        HashSet<Guid> shown)
    {
        if (transaction.Transfer?.PartnerTransactionId is not { } partnerId || shown.Contains(partnerId))
        {
            return null;
        }

        if (!byId.TryGetValue(partnerId, out Transaction? partner))
        {
            return null;
        }

        // Only one that points back is part of the same pair
        return partner.Transfer?.PartnerTransactionId == transaction.TransactionId ? partner : null;
    }
}
