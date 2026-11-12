using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

/// <summary>
/// The people whose transactions are matched together: a user, and everyone joined to them through accepted links
/// that share accounts, and theirs in turn. The same group whoever in it is matched, so every run comes to the same
/// decisions about a pair that spans two of them.
/// </summary>
/// <param name="UserIds">Everyone in the group, the user included.</param>
/// <param name="Pairing">Which of their accounts can pair across a link.</param>
internal sealed record TransferGroup(IReadOnlyList<Guid> UserIds, TransferPairing Pairing)
{
    /// <summary>
    /// <paramref name="userId"/>'s group, just them when nothing's shared with or by them.
    /// </summary>
    public static async Task<TransferGroup> ForAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken)
    {
        // Every account share in an accepted link, while the account is still its sharer's. A handful per household,
        // so they're all read and the group worked out here
        var shares = await (
                from share in context.Shares
                join link in context.UserLinks on share.LinkId equals link.Id
                join account in context.BankAccounts on share.ItemId equals account.Id
                where share.ItemType == ShareItemType.BankAccount
                      && link.Status == UserLinkStatus.Accepted
                      && account.UserId == share.UserId
                select new { AccountId = account.Id, SharedBy = share.UserId, link.UserId, link.InviteeId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        HashSet<Guid> group = [userId];
        Queue<Guid> toVisit = new([userId]);
        while (toVisit.TryDequeue(out Guid member))
        {
            foreach (var share in shares.Where(x => x.UserId == member || x.InviteeId == member))
            {
                Guid other = share.UserId == member ? share.InviteeId : share.UserId;
                if (group.Add(other))
                {
                    toVisit.Enqueue(other);
                }
            }
        }

        IEnumerable<(Guid AccountId, Guid SharedWith)> pairs = shares
            .Where(x => group.Contains(x.SharedBy))
            .Select(static x => (x.AccountId, x.SharedBy == x.UserId ? x.InviteeId : x.UserId));

        return new TransferGroup(group.ToList(), new TransferPairing(pairs));
    }
}
