using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Transactions;

/// <summary>
/// The names of the custom classifications that transactions in a view can carry.
/// </summary>
internal static class TagNames
{
    /// <summary>
    /// Tag names by id the user's own, those of whoever shares accounts in <paramref name="view"/>, and those the
    /// other person put on the user's transactions. Only those on transactions in view ever show.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> LoadAsync(FinanceTrackerContext context, AccountsInView view,
        CancellationToken cancellationToken)
    {
        IQueryable<Guid> shared = context.SharedWith(view.UserId, ShareItemType.Classification);

        return await context.CustomClassifications
            .OwnedByAnyOrShared([view.UserId, .. view.SharedOwnerIds], shared)
            .AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
    }
}
