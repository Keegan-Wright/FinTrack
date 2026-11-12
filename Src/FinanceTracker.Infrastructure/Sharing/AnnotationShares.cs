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

        bool shared = context.Shares.Local.Any(x => IsShare(x, linkId, itemType, itemId)) ||
                      await context.Shares.AnyAsync(
                          x => x.LinkId == linkId && x.ItemType == itemType && x.ItemId == itemId, cancellationToken);
        if (shared)
        {
            return;
        }

        context.Shares.Add(new Share(by, linkId, itemType, itemId, ShareMode.ReadOnly, now));
    }

    private static bool IsShare(Share share, Guid linkId, ShareItemType itemType, Guid itemId) =>
        share.LinkId == linkId && share.ItemType == itemType && share.ItemId == itemId;
}
