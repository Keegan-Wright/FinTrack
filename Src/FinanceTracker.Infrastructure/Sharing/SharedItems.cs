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

        /// <summary>
        /// The <paramref name="itemType"/>s the other person in one of <paramref name="userId"/>'s accepted links chose
        /// to share with them, with how, and who shares each. Not those only shared for being on their transaction.
        /// </summary>
        public IQueryable<Share> ChosenSharesWith(Guid userId, ShareItemType itemType) =>
            from share in context.Shares
            join link in context.UserLinks on share.LinkId equals link.Id
            where share.ItemType == itemType
                  && share.Reason == ShareReason.Chosen
                  && share.UserId != userId
                  && link.Status == UserLinkStatus.Accepted
                  && (link.UserId == userId || link.InviteeId == userId)
            select share;
    }
}
