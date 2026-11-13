using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;
using DomainShareMode = FinanceTracker.Domain.Sharing.ShareMode;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The things besides accounts a user chooses to share in a link, see <see cref="SharedItemKind"/>. A bill, or a debt
/// following an account, goes with that account when it's shared, as it is, rather than being chosen on its own.
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
        Dictionary<(ShareItemType, Guid), Guid> accountOf = (await AccountItemsAsync(context, userId,
                cancellationToken))
            .ToDictionary(static x => (x.ItemType, x.Id), static x => x.AccountId);

        List<ShareableItem> items = [];
        foreach ((ShareItemType itemType, Guid id, string name) in await ShareableOfAsync(context, userId,
                     cancellationToken))
        {
            // Goes with a shared account, and is shared as it is
            if (accountOf.TryGetValue((itemType, id), out Guid accountId) &&
                sharedAccounts.TryGetValue(accountId, out (string Name, DomainShareMode Mode) account))
            {
                items.Add(new ShareableItem
                {
                    Kind = ToContract(itemType),
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
    /// <paramref name="userId"/>'s bills and debts going with an account they share in <paramref name="link"/>, so
    /// shared with it.
    /// </summary>
    public static async Task<HashSet<(ShareItemType ItemType, Guid Id)>> FollowingAccountsAsync(
        FinanceTrackerContext context, DomainLink link, Guid userId, CancellationToken cancellationToken)
    {
        HashSet<Guid> accountIds = (await SharedAccountsAsync(context, link, userId, cancellationToken)).Keys
            .ToHashSet();
        return (await AccountItemsAsync(context, userId, cancellationToken))
            .Where(x => accountIds.Contains(x.AccountId))
            .Select(static x => (x.ItemType, x.Id))
            .ToHashSet();
    }

    /// <summary>
    /// Takes <paramref name="userId"/>'s bills and debts that stopped being shared in <paramref name="link"/>, as the
    /// account they go with no longer is, off the other person's transactions. Not those still shared another way,
    /// chosen or for being on their transaction. Tracked changes, saved by the caller.
    /// </summary>
    /// <param name="before">The bills and debts that went with a shared account before the change.</param>
    public static async Task TakeOffNoLongerSharedAsync(FinanceTrackerContext context, DomainLink link, Guid userId,
        HashSet<(ShareItemType ItemType, Guid Id)> before, CancellationToken cancellationToken)
    {
        HashSet<(ShareItemType ItemType, Guid Id)> after =
            await FollowingAccountsAsync(context, link, userId, cancellationToken);
        HashSet<(ShareItemType ItemType, Guid Id)> stillShared = (await context.Shares.AsNoTracking()
                .Where(x => x.LinkId == link.Id && x.UserId == userId && x.ItemType != ShareItemType.BankAccount)
                .Select(static x => new { x.ItemType, x.ItemId })
                .ToListAsync(cancellationToken))
            .Select(static x => (x.ItemType, x.ItemId))
            .ToHashSet();

        foreach ((ShareItemType itemType, Guid id) in before.Except(after).Except(stillShared))
        {
            await AnnotationShares.TakeOffTransactionsOfAsync(context, link.OtherThan(userId), itemType, id,
                cancellationToken);
        }
    }

    /// <summary>
    /// The things besides accounts the other person in <paramref name="link"/> shares with <paramref name="userId"/>,
    /// chosen or going with an account they share, by kind then name.
    /// </summary>
    public static async Task<List<ItemSharedWithYou>> TheirsAsync(FinanceTrackerContext context, DomainLink link,
        Guid userId, CancellationToken cancellationToken)
    {
        Guid otherId = link.OtherThan(userId);

        // Only what they share, while still theirs. Names may be encrypted, so they're sorted once read
        Dictionary<(ShareItemType, Guid), DomainShareMode> chosen = await context.Shares.AsNoTracking()
            .Where(x => x.LinkId == link.Id && x.UserId == otherId && x.Reason == ShareReason.Chosen &&
                        x.ItemType != ShareItemType.BankAccount)
            .ToDictionaryAsync(static x => (x.ItemType, x.ItemId), static x => x.Mode, cancellationToken);
        Dictionary<Guid, (string Name, DomainShareMode Mode)> theirAccounts =
            await SharedAccountsAsync(context, link, otherId, cancellationToken);
        Dictionary<(ShareItemType, Guid), Guid> accountOf = (await AccountItemsAsync(context, otherId,
                cancellationToken))
            .ToDictionary(static x => (x.ItemType, x.Id), static x => x.AccountId);

        List<(SharedItemKind Kind, string Name, DomainShareMode Mode)> shared = [];
        foreach ((ShareItemType itemType, Guid id, string name) in await ShareableOfAsync(context, otherId,
                     cancellationToken))
        {
            if (ModeSharedWithThem(chosen, theirAccounts, accountOf, itemType, id) is { } mode)
            {
                shared.Add((ToContract(itemType), name, mode));
            }
        }

        return shared
            .Select(static x => new ItemSharedWithYou
            {
                Kind = x.Kind, Name = x.Name, Mode = ShareModeMapping.ToContract(x.Mode)
            })
            .ToList();
    }

    /// <summary>
    /// How one of the other person's things is shared with the user, the more allowing of chosen and going with an
    /// account, null when it isn't.
    /// </summary>
    private static DomainShareMode? ModeSharedWithThem(Dictionary<(ShareItemType, Guid), DomainShareMode> chosen,
        Dictionary<Guid, (string Name, DomainShareMode Mode)> theirAccounts,
        Dictionary<(ShareItemType, Guid), Guid> accountOf, ShareItemType itemType, Guid id)
    {
        DomainShareMode? mode = null;
        if (chosen.TryGetValue((itemType, id), out DomainShareMode chosenMode))
        {
            mode = chosenMode;
        }

        if (accountOf.TryGetValue((itemType, id), out Guid accountId) &&
            theirAccounts.TryGetValue(accountId, out (string Name, DomainShareMode Mode) account) &&
            mode != DomainShareMode.AllowChanges)
        {
            mode = account.Mode;
        }

        return mode;
    }

    /// <summary>
    /// Replaces what <paramref name="userId"/> chooses to share in <paramref name="link"/> besides accounts with
    /// <paramref name="request"/>. Something no longer shared comes off the other person's transactions, unless it's
    /// shared for being on them anyway, or still goes with a shared account. Tracked changes, saved by the caller.
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
            throw new DomainRuleException("Only your own things can be shared.");
        }

        // What goes with an account shared here isn't chosen on its own
        HashSet<(ShareItemType ItemType, Guid Id)> followingAccounts =
            await FollowingAccountsAsync(context, link, userId, cancellationToken);
        Dictionary<(ShareItemType, Guid), DomainShareMode> wanted = request.Items
            .Where(x => !followingAccounts.Contains((ToDomain(x.Kind), x.ItemId)))
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
            bool stillWithItsAccount = followingAccounts.Contains((share.ItemType, share.ItemId));
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
    /// <paramref name="userId"/>'s things that go with an account. Their bills, each paid from one, and their debts
    /// following one.
    /// </summary>
    private static async Task<List<(ShareItemType ItemType, Guid Id, Guid AccountId)>> AccountItemsAsync(
        FinanceTrackerContext context, Guid userId, CancellationToken cancellationToken)
    {
        var bills = await context.Bills.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, x.AccountId })
            .ToListAsync(cancellationToken);

        var debts = await context.Debts.OwnedBy(userId).AsNoTracking()
            .Where(static x => x.AccountId != null)
            .Select(static x => new { x.Id, AccountId = x.AccountId!.Value })
            .ToListAsync(cancellationToken);

        var goals = await context.SavingsGoals.OwnedBy(userId).AsNoTracking()
            .Where(static x => x.Source == Domain.SavingsGoals.SavingsGoalSource.Account && x.AccountId != null)
            .Select(static x => new { x.Id, AccountId = x.AccountId!.Value })
            .ToListAsync(cancellationToken);

        return
        [
            .. bills.Select(static x => (ShareItemType.Bill, x.Id, x.AccountId)),
            .. debts.Select(static x => (ShareItemType.Debt, x.Id, x.AccountId)),
            .. goals.Select(static x => (ShareItemType.SavingsGoal, x.Id, x.AccountId))
        ];
    }

    private static ShareItemType ToDomain(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Classification => ShareItemType.Classification,
        SharedItemKind.Rule => ShareItemType.Rule,
        SharedItemKind.Bill => ShareItemType.Bill,
        SharedItemKind.Debt => ShareItemType.Debt,
        SharedItemKind.Budget => ShareItemType.Budget,
        SharedItemKind.SavingsGoal => ShareItemType.SavingsGoal,
        SharedItemKind.CalendarEvent => ShareItemType.CalendarEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static SharedItemKind ToContract(ShareItemType itemType) => itemType switch
    {
        ShareItemType.Classification => SharedItemKind.Classification,
        ShareItemType.Rule => SharedItemKind.Rule,
        ShareItemType.Bill => SharedItemKind.Bill,
        ShareItemType.Debt => SharedItemKind.Debt,
        ShareItemType.Budget => SharedItemKind.Budget,
        ShareItemType.SavingsGoal => SharedItemKind.SavingsGoal,
        ShareItemType.CalendarEvent => SharedItemKind.CalendarEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, null)
    };

    /// <summary>
    /// What <paramref name="userId"/> can share besides accounts: their tags, rules, bills, debts, budgets, goals and
    /// calendar events, each by name.
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
        var debts = await context.Debts.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        var budgets = await context.Budgets.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        var goals = await context.SavingsGoals.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        var events = await context.CalendarEvents.OwnedBy(userId).AsNoTracking()
            .Select(static x => new { x.Id, Name = x.Title })
            .ToListAsync(cancellationToken);

        return
        [
            .. ByName(ShareItemType.Classification, tags.Select(static x => (x.Id, x.Name))),
            .. ByName(ShareItemType.Rule, rules.Select(static x => (x.Id, x.Name))),
            .. ByName(ShareItemType.Bill, bills.Select(static x => (x.Id, x.Name))),
            .. ByName(ShareItemType.Debt, debts.Select(static x => (x.Id, x.Name))),
            .. ByName(ShareItemType.Budget, budgets.Select(static x => (x.Id, x.Name))),
            .. ByName(ShareItemType.SavingsGoal, goals.Select(static x => (x.Id, x.Name))),
            .. ByName(ShareItemType.CalendarEvent, events.Select(static x => (x.Id, x.Name)))
        ];
    }

    private static IEnumerable<(ShareItemType ItemType, Guid Id, string Name)> ByName(ShareItemType itemType,
        IEnumerable<(Guid Id, string Name)> items) =>
        items.OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => (itemType, x.Id, x.Name));

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
