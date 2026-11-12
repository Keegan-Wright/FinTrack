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
        Dictionary<Guid, (string Name, DomainShareMode Mode)> sharedAccounts =
            await SharedAccountsAsync(context, link, userId, cancellationToken);
        Dictionary<Guid, Guid> billAccounts = await context.Bills.OwnedBy(userId).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, static x => x.AccountId, cancellationToken);

        List<ShareableItem> items = [];
        foreach ((ShareItemType itemType, Guid id, string name) in await ShareableOfAsync(context, userId,
                     cancellationToken))
        {
            // A bill paid from a shared account goes with it, and is shared as it is
            if (itemType == ShareItemType.Bill &&
                sharedAccounts.TryGetValue(billAccounts[id], out (string Name, DomainShareMode Mode) account))
            {
                items.Add(new ShareableItem
                {
                    Kind = SharedItemKind.Bill,
                    ItemId = id,
                    Name = name,
                    Mode = ShareModeMapping.ToContract(account.Mode),
                    FollowsAccount = account.Name
                });
                continue;
            }

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
    /// <paramref name="userId"/>'s bills paid from an account they share in <paramref name="link"/>, so shared with it.
    /// </summary>
    public static async Task<HashSet<Guid>> BillsFollowingAccountsAsync(FinanceTrackerContext context,
        DomainLink link, Guid userId, CancellationToken cancellationToken)
    {
        List<Guid> accountIds = (await SharedAccountsAsync(context, link, userId, cancellationToken)).Keys.ToList();
        List<Guid> bills = await context.Bills.OwnedBy(userId).AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId))
            .Select(static x => x.Id)
            .ToListAsync(cancellationToken);

        return bills.ToHashSet();
    }

    /// <summary>
    /// Takes <paramref name="userId"/>'s bills that stopped being shared in <paramref name="link"/>, as the account
    /// they're paid from no longer is, off the other person's transactions. Not those still shared another way, chosen
    /// or for being on their transaction. Tracked changes, saved by the caller.
    /// </summary>
    /// <param name="before">The bills that went with a shared account before the change.</param>
    public static async Task TakeOffBillsNoLongerSharedAsync(FinanceTrackerContext context, DomainLink link,
        Guid userId, HashSet<Guid> before, CancellationToken cancellationToken)
    {
        HashSet<Guid> after = await BillsFollowingAccountsAsync(context, link, userId, cancellationToken);
        List<Guid> stillShared = await context.Shares.AsNoTracking()
            .Where(x => x.LinkId == link.Id && x.UserId == userId && x.ItemType == ShareItemType.Bill)
            .Select(static x => x.ItemId)
            .ToListAsync(cancellationToken);

        foreach (Guid billId in before.Except(after).Except(stillShared))
        {
            await AnnotationShares.TakeOffTransactionsOfAsync(context, link.OtherThan(userId), ShareItemType.Bill,
                billId, cancellationToken);
        }
    }

    /// <summary>
    /// The accounts <paramref name="userId"/> shares in <paramref name="link"/>, with their names and how each is
    /// shared, by account.
    /// </summary>
    private static async Task<Dictionary<Guid, (string Name, DomainShareMode Mode)>> SharedAccountsAsync(
        FinanceTrackerContext context, DomainLink link, Guid userId, CancellationToken cancellationToken)
    {
        var shared = await (
                from share in context.Shares
                join account in context.BankAccounts on share.ItemId equals account.Id
                where share.LinkId == link.Id && share.UserId == userId &&
                      share.ItemType == ShareItemType.BankAccount && account.UserId == userId
                select new { account.Id, account.DisplayName, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return shared.ToDictionary(static x => x.Id, static x => (x.DisplayName, x.Mode));
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

        // Bills they chose to share, and those paid from an account they share, as that account is
        var chosenBills = await (
                from share in context.Shares
                join bill in context.Bills on share.ItemId equals bill.Id
                where share.LinkId == link.Id && share.UserId == otherId && share.Reason == ShareReason.Chosen &&
                      share.ItemType == ShareItemType.Bill && bill.UserId == otherId
                select new { bill.Id, bill.Name, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Dictionary<Guid, (string Name, DomainShareMode Mode)> theirAccounts =
            await SharedAccountsAsync(context, link, otherId, cancellationToken);
        List<Guid> theirAccountIds = theirAccounts.Keys.ToList();

        var accountBills = await context.Bills.OwnedBy(otherId).AsNoTracking()
            .Where(x => theirAccountIds.Contains(x.AccountId))
            .Select(static x => new { x.Id, x.Name, x.AccountId })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, (string Name, DomainShareMode Mode)> bills = chosenBills
            .ToDictionary(static x => x.Id, static x => (x.Name, x.Mode));
        foreach (var bill in accountBills)
        {
            DomainShareMode mode = theirAccounts[bill.AccountId].Mode;
            if (bills.TryGetValue(bill.Id, out (string Name, DomainShareMode Mode) chosen) &&
                chosen.Mode == DomainShareMode.AllowChanges)
            {
                mode = DomainShareMode.AllowChanges;
            }

            bills[bill.Id] = (bill.Name, mode);
        }

        return
        [
            .. Named(SharedItemKind.Classification, tags.Select(static x => (x.Name, x.Mode))),
            .. Named(SharedItemKind.Rule, rules.Select(static x => (x.Name, x.Mode))),
            .. Named(SharedItemKind.Bill, bills.Values)
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

        // A bill paid from an account shared here goes with the account, it isn't chosen on its own
        HashSet<Guid> followingAccounts = await BillsFollowingAccountsAsync(context, link, userId, cancellationToken);
        Dictionary<(ShareItemType, Guid), DomainShareMode> wanted = request.Items
            .Where(x => !(x.Kind == SharedItemKind.Bill && followingAccounts.Contains(x.ItemId)))
            .ToDictionary(static x => (ToDomain(x.Kind), x.ItemId), static x => ShareModeMapping.ToDomain(x.Mode));

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
            bool stillWithItsAccount = share.ItemType == ShareItemType.Bill && followingAccounts.Contains(share.ItemId);
            if (!stillOnTheirs && !stillWithItsAccount)
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
        SharedItemKind.Bill => ShareItemType.Bill,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static SharedItemKind ToContract(ShareItemType itemType) => itemType switch
    {
        ShareItemType.Classification => SharedItemKind.Classification,
        ShareItemType.Rule => SharedItemKind.Rule,
        ShareItemType.Bill => SharedItemKind.Bill,
        _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, null)
    };

    /// <summary>
    /// What <paramref name="userId"/> can choose to share besides accounts: their tags, rules and bills, each by name.
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
        var bills = await context.Bills.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        return
        [
            .. tags.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(static x => (ShareItemType.Classification, x.Id, x.Name)),
            .. rules.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(static x => (ShareItemType.Rule, x.Id, x.Name)),
            .. bills.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(static x => (ShareItemType.Bill, x.Id, x.Name))
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
