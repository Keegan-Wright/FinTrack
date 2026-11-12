using FinanceTracker.Domain.Sharing;

namespace FinanceTracker.Infrastructure.Sharing;

public static class SharedItems
{
    extension(FinanceTrackerContext context)
    {
        /// <summary>
        /// The ids of the <paramref name="itemType"/>s the other person in one of <paramref name="userId"/>'s accepted
        /// links shares with them. A query, to be used inside another.
        /// </summary>
        public IQueryable<Guid> SharedWith(Guid userId, ShareItemType itemType) =>
            from share in context.Shares
            join link in context.UserLinks on share.LinkId equals link.Id
            where share.ItemType == itemType
                  && share.UserId != userId
                  && link.Status == UserLinkStatus.Accepted
                  && (link.UserId == userId || link.InviteeId == userId)
            select share.ItemId;
    }
}
