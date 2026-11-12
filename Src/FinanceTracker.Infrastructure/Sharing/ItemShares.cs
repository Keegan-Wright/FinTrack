using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;
using DomainShareMode = FinanceTracker.Domain.Sharing.ShareMode;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The things besides accounts a user chooses to share in a link, see <see cref="SharedItemKind"/>.
/// </summary>
internal static class ItemShares
{
    /// <summary>
    /// Every one of <paramref name="userId"/>'s own things that can be shared, with how each is shared in
    /// <paramref name="link"/>, by kind then name.
    /// </summary>
    public static async Task<List<ShareableItem>> YoursAsync(FinanceTrackerContext context, DomainLink link,
        Guid userId, CancellationToken cancellationToken)
    {
        Dictionary<(ShareItemType, Guid), DomainShareMode> chosen = await context.Shares.AsNoTracking()
            .Where(x => x.LinkId == link.Id && x.UserId == userId && x.Reason == ShareReason.Chosen &&
                        x.ItemType != ShareItemType.BankAccount)
            .ToDictionaryAsync(static x => (x.ItemType, x.ItemId), static x => x.Mode, cancellationToken);

        List<ShareableItem> items = [];
        foreach ((Guid id, string name) in await TagsOfAsync(context, userId, cancellationToken))
        {
            items.Add(new ShareableItem
            {
                Kind = SharedItemKind.Classification,
                ItemId = id,
                Name = name,
                Mode = ModeIfChosen(chosen, ShareItemType.Classification, id)
            });
        }

        return items;
    }

    /// <summary>
    /// The things besides accounts the other person in <paramref name="link"/> chose to share with
    /// <paramref name="userId"/>, by kind then name.
    /// </summary>
    public static async Task<List<ItemSharedWithYou>> TheirsAsync(FinanceTrackerContext context, DomainLink link,
        Guid userId, CancellationToken cancellationToken)
    {
        Guid otherId = link.OtherThan(userId);

        // Only the tags they share, while still theirs. Names are encrypted, so they're sorted once read
        var shared = await (
                from share in context.Shares
                join tag in context.CustomClassifications on share.ItemId equals tag.Id
                where share.LinkId == link.Id && share.UserId == otherId && share.Reason == ShareReason.Chosen &&
                      share.ItemType == ShareItemType.Classification && tag.UserId == otherId
                select new { tag.Tag, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return shared.OrderBy(static x => x.Tag, StringComparer.CurrentCultureIgnoreCase)
            .Select(static x => new ItemSharedWithYou
            {
                Kind = SharedItemKind.Classification, Name = x.Tag, Mode = ShareModeMapping.ToContract(x.Mode)
            })
            .ToList();
    }

    /// <summary>
    /// Replaces what <paramref name="userId"/> chooses to share in <paramref name="link"/> besides accounts with
    /// <paramref name="request"/>. A tag no longer shared comes off the other person's transactions, unless it's
    /// shared for being on them anyway. Tracked changes, saved by the caller.
    /// </summary>
    /// <exception cref="InvalidOperationException">Something in the request isn't theirs.</exception>
    public static async Task ChangeAsync(FinanceTrackerContext context, DomainLink link, Guid userId,
        ChangeItemShares request, DateTime now, CancellationToken cancellationToken)
    {
        HashSet<Guid> ownTags = (await TagsOfAsync(context, userId, cancellationToken))
            .Select(static x => x.Id)
            .ToHashSet();
        if (request.Items.Any(x => x.Kind == SharedItemKind.Classification && !ownTags.Contains(x.ItemId)))
        {
            throw new InvalidOperationException("Only your own tags can be shared.");
        }

        Dictionary<(ShareItemType, Guid), DomainShareMode> wanted = request.Items.ToDictionary(
            static x => (ToDomain(x.Kind), x.ItemId), static x => ShareModeMapping.ToDomain(x.Mode));

        List<Share> existing = await context.Shares
            .Where(x => x.LinkId == link.Id && x.UserId == userId && x.ItemType != ShareItemType.BankAccount)
            .ToListAsync(cancellationToken);

        foreach (Share share in existing.Where(static x => x.Reason == ShareReason.Chosen))
        {
            if (wanted.Remove((share.ItemType, share.ItemId), out DomainShareMode mode))
            {
                share.ChangeMode(mode);
                continue;
            }

            context.Shares.Remove(share);
            bool stillOnTheirs = existing.Exists(x => x.Reason == ShareReason.OnTransaction &&
                                                      x.ItemType == share.ItemType && x.ItemId == share.ItemId);
            if (!stillOnTheirs)
            {
                await AnnotationShares.TakeOffTransactionsOfAsync(context, link.OtherThan(userId), share.ItemType,
                    share.ItemId, cancellationToken);
            }
        }

        foreach (((ShareItemType itemType, Guid itemId), DomainShareMode mode) in wanted)
        {
            context.Shares.Add(new Share(userId, link.Id, itemType, itemId, mode, now));
        }
    }

    private static ShareItemType ToDomain(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Classification => ShareItemType.Classification,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>
    /// <paramref name="userId"/>'s tags by name.
    /// </summary>
    private static async Task<List<(Guid Id, string Name)>> TagsOfAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken)
    {
        var tags = await context.CustomClassifications.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, x.Tag })
            .ToListAsync(cancellationToken);

        return tags.OrderBy(static x => x.Tag, StringComparer.CurrentCultureIgnoreCase)
            .Select(static x => (x.Id, x.Tag))
            .ToList();
    }

    private static Contracts.Sharing.ShareMode? ModeIfChosen(Dictionary<(ShareItemType, Guid), DomainShareMode> chosen,
        ShareItemType itemType, Guid itemId)
    {
        if (chosen.TryGetValue((itemType, itemId), out DomainShareMode mode))
        {
            return ShareModeMapping.ToContract(mode);
        }

        return null;
    }
}
