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
        foreach ((ShareItemType itemType, Guid id, string name) in await ShareableOfAsync(context, userId,
                     cancellationToken))
        {
            items.Add(new ShareableItem
            {
                Kind = ToContract(itemType),
                ItemId = id,
                Name = name,
                Mode = ModeIfChosen(chosen, itemType, id)
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

        // Only what they share, while still theirs. Names may be encrypted, so they're sorted once read
        var tags = await (
                from share in context.Shares
                join tag in context.CustomClassifications on share.ItemId equals tag.Id
                where share.LinkId == link.Id && share.UserId == otherId && share.Reason == ShareReason.Chosen &&
                      share.ItemType == ShareItemType.Classification && tag.UserId == otherId
                select new { Name = tag.Tag, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var rules = await (
                from share in context.Shares
                join rule in context.Rules on share.ItemId equals rule.Id
                where share.LinkId == link.Id && share.UserId == otherId && share.Reason == ShareReason.Chosen &&
                      share.ItemType == ShareItemType.Rule && rule.UserId == otherId
                select new { rule.Name, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return
        [
            .. Named(SharedItemKind.Classification, tags.Select(static x => (x.Name, x.Mode))),
            .. Named(SharedItemKind.Rule, rules.Select(static x => (x.Name, x.Mode)))
        ];
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
        HashSet<(ShareItemType, Guid)> own = (await ShareableOfAsync(context, userId, cancellationToken))
            .Select(static x => (x.ItemType, x.Id))
            .ToHashSet();
        if (request.Items.Any(x => !own.Contains((ToDomain(x.Kind), x.ItemId))))
        {
            throw new InvalidOperationException("Only your own things can be shared.");
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
        SharedItemKind.Rule => ShareItemType.Rule,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static SharedItemKind ToContract(ShareItemType itemType) => itemType switch
    {
        ShareItemType.Classification => SharedItemKind.Classification,
        ShareItemType.Rule => SharedItemKind.Rule,
        _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, null)
    };

    /// <summary>
    /// What <paramref name="userId"/> can choose to share besides accounts: their tags, then their rules, each by name.
    /// </summary>
    private static async Task<List<(ShareItemType ItemType, Guid Id, string Name)>> ShareableOfAsync(
        FinanceTrackerContext context, Guid userId, CancellationToken cancellationToken)
    {
        var tags = await context.CustomClassifications.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, Name = x.Tag })
            .ToListAsync(cancellationToken);
        var rules = await context.Rules.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        return
        [
            .. tags.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(static x => (ShareItemType.Classification, x.Id, x.Name)),
            .. rules.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(static x => (ShareItemType.Rule, x.Id, x.Name))
        ];
    }

    /// <summary>
    /// <paramref name="items"/> of <paramref name="kind"/> shared with the user, by name.
    /// </summary>
    private static IEnumerable<ItemSharedWithYou> Named(SharedItemKind kind,
        IEnumerable<(string Name, DomainShareMode Mode)> items) =>
        items.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new ItemSharedWithYou
            {
                Kind = kind, Name = x.Name, Mode = ShareModeMapping.ToContract(x.Mode)
            });

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
