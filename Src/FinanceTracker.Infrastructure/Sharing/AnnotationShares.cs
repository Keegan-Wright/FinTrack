using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Shares what one person puts on the other's transaction, such as their tag or bill, with the transaction's owner,
/// read only, so the owner sees its name. It goes with the link, see <see cref="SharingService.EndAsync"/>.
/// </summary>
internal static class AnnotationShares
{
    /// <summary>
    /// Shares <paramref name="itemId"/> with <paramref name="transaction"/>'s owner through their link with
    /// <paramref name="by"/>, unless it's already shared or <paramref name="by"/> is the owner. Added to the context,
    /// saved with the change.
    /// </summary>
    /// <param name="by">Who put it on, and owns it.</param>
    /// <exception cref="InvalidOperationException">They aren't linked, so couldn't be changing it.</exception>
    public static async Task ShareWithOwnerAsync(FinanceTrackerContext context, Guid by, BankTransaction transaction,
        ShareItemType itemType, Guid itemId, DateTime now, CancellationToken cancellationToken)
    {
        Guid ownerId = transaction.UserId;
        if (ownerId == by)
        {
            return;
        }

        Guid linkId = await context.UserLinks
                          .Where(x => x.Status == UserLinkStatus.Accepted &&
                                      ((x.UserId == by && x.InviteeId == ownerId) ||
                                       (x.UserId == ownerId && x.InviteeId == by)))
                          .Select(static x => (Guid?)x.Id)
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Transaction {transaction.Id} belongs to someone not linked with the user.");

        // Kept apart from a share they chose, which they can take back without it coming off the transaction
        bool shared = context.Shares.Local.Any(x => IsShare(x, linkId, itemType, itemId)) ||
                      await context.Shares.AnyAsync(
                          x => x.LinkId == linkId && x.ItemType == itemType && x.ItemId == itemId &&
                               x.Reason == ShareReason.OnTransaction, cancellationToken);
        if (shared)
        {
            return;
        }

        context.Shares.Add(new Share(by, linkId, itemType, itemId, ShareMode.ReadOnly, now,
            ShareReason.OnTransaction));
    }

    /// <summary>
    /// Takes the tag, bill or debt <paramref name="itemId"/> off every transaction of <paramref name="ownerId"/>'s, as
    /// when they can no longer see it. Tracked changes, saved by the caller.
    /// </summary>
    public static async Task TakeOffTransactionsOfAsync(FinanceTrackerContext context, Guid ownerId,
        ShareItemType itemType, Guid itemId, CancellationToken cancellationToken)
    {
        List<BankTransaction> carrying = await Carrying(context.BankTransactions.OwnedBy(ownerId), itemType, itemId)
            .ToListAsync(cancellationToken);
        foreach (BankTransaction transaction in carrying)
        {
            TakeOff(transaction, itemType, itemId);
        }
    }

    /// <summary>
    /// Those of <paramref name="transactions"/> that carry the tag, bill or debt <paramref name="itemId"/>.
    /// </summary>
    private static IQueryable<BankTransaction> Carrying(IQueryable<BankTransaction> transactions,
        ShareItemType itemType, Guid itemId) => itemType switch
    {
        ShareItemType.Classification => transactions.Where(x => x.CustomClassificationIds.Contains(itemId)),
        ShareItemType.Bill => transactions.Where(x => x.BillPayment != null && x.BillPayment.BillId == itemId),
        ShareItemType.Debt => transactions.Where(x => x.DebtPayment != null && x.DebtPayment.DebtId == itemId),
        _ => transactions.Where(static _ => false)
    };

    /// <summary>
    /// Takes the tag, bill or debt <paramref name="itemId"/> off <paramref name="transaction"/>, as if it had been
    /// deleted, so matching and rules can decide about it again.
    /// </summary>
    private static void TakeOff(BankTransaction transaction, ShareItemType itemType, Guid itemId)
    {
        switch (itemType)
        {
            case ShareItemType.Classification:
                transaction.RemoveDeletedClassification(itemId);
                break;
            case ShareItemType.Bill:
                transaction.RemoveDeletedBill(itemId);
                break;
            case ShareItemType.Debt:
                transaction.RemoveDeletedDebt(itemId);
                break;
        }
    }

    private static bool IsShare(Share share, Guid linkId, ShareItemType itemType, Guid itemId) =>
        share.LinkId == linkId && share.ItemType == itemType && share.ItemId == itemId &&
        share.Reason == ShareReason.OnTransaction;
}
