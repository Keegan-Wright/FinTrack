using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The things of one kind a user sees that are only ever shared by choice. Their own, and those another person chose
/// to share with them, such as budgets and calendar events.
/// </summary>
internal static class SharedChosenItems
{
    /// <summary>
    /// Every one of <paramref name="items"/> <paramref name="userId"/> sees, untracked, their own first.
    /// </summary>
    /// <param name="items">All of the kind, such as <c>context.Budgets</c>.</param>
    /// <param name="itemType">Which kind they are, as shares name it.</param>
    /// <param name="justMine">Their Just mine view, which leaves out what's shared with them.</param>
    public static async Task<List<VisibleItem<T>>> ForAsync<T>(FinanceTrackerContext context, IQueryable<T> items,
        Guid userId, ShareItemType itemType, bool justMine, CancellationToken cancellationToken)
        where T : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(context);
        List<T> own = await items.Where(x => x.UserId == userId).AsNoTracking().ToListAsync(cancellationToken);
        if (justMine)
        {
            return own.Select(static x => new VisibleItem<T>(x, null, true)).ToList();
        }

        var chosen = await (
                from share in context.ChosenSharesWith(userId, itemType)
                join item in items on share.ItemId equals item.Id
                where item.UserId == share.UserId
                select new { Item = item, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<Guid> ownerIds = chosen.Select(static x => x.Item.UserId).Distinct().ToList();
        Dictionary<Guid, string> names = await OwnerNamesAsync(context, ownerIds, cancellationToken);

        return
        [
            .. own.Select(static x => new VisibleItem<T>(x, null, true)),
            .. chosen.Select(x => new VisibleItem<T>(x.Item, names.GetValueOrDefault(x.Item.UserId, string.Empty),
                x.Mode == ShareMode.AllowChanges))
        ];
    }

    /// <summary>
    /// <paramref name="chosen"/> with <paramref name="following"/>, other people's that go with an account shared with
    /// the user, shared the same way as it. One that's both chosen and following an account can be changed when either
    /// allows it. Their own first.
    /// </summary>
    /// <param name="chosen">What <see cref="ForAsync{T}"/> found.</param>
    /// <param name="following">Other people's on the accounts in <paramref name="view"/> shared with the user.</param>
    /// <param name="accountOf">The account each of <paramref name="following"/> goes with.</param>
    public static async Task<List<VisibleItem<T>>> WithFollowingAsync<T>(FinanceTrackerContext context,
        List<VisibleItem<T>> chosen, List<T> following, Func<T, Guid> accountOf, AccountsInView view,
        CancellationToken cancellationToken)
        where T : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(following);
        ArgumentNullException.ThrowIfNull(accountOf);
        ArgumentNullException.ThrowIfNull(view);

        Dictionary<Guid, VisibleItem<T>> byId = chosen.ToDictionary(static x => x.Item.Id);
        List<Guid> ownerIds = following.Select(static x => x.UserId).Distinct().ToList();
        Dictionary<Guid, string> names = await OwnerNamesAsync(context, ownerIds, cancellationToken);
        foreach (T item in following)
        {
            bool canChange = view.ChangeableAccountIds.Contains(accountOf(item));
            if (byId.TryGetValue(item.Id, out VisibleItem<T>? already))
            {
                canChange |= already.CanChange;
            }

            byId[item.Id] = new VisibleItem<T>(item, names.GetValueOrDefault(item.UserId, string.Empty), canChange);
        }

        return [.. byId.Values.OrderBy(static x => !x.IsOwn)];
    }

    /// <summary>
    /// The first and last names of <paramref name="ownerIds"/>, by user.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> OwnerNamesAsync(FinanceTrackerContext context,
        IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await context.Users.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => $"{x.FirstName} {x.LastName}".Trim(), cancellationToken);
    }

    /// <summary>
    /// Whether <paramref name="userId"/> chose to share their <paramref name="itemId"/> with anyone they're linked
    /// with.
    /// </summary>
    public static Task<bool> IsSharedAsync(FinanceTrackerContext context, Guid userId, ShareItemType itemType,
        Guid itemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (
                from share in context.Shares
                join link in context.UserLinks on share.LinkId equals link.Id
                where share.UserId == userId && share.ItemType == itemType && share.ItemId == itemId &&
                      share.Reason == ShareReason.Chosen && link.Status == UserLinkStatus.Accepted
                select share.Id)
            .AnyAsync(cancellationToken);
    }
}
