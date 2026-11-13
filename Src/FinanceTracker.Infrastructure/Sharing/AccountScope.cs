using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Sharing;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAccountScope>]
public sealed class AccountScope : IAccountScope
{
    private readonly ICurrentUser _currentUser;

    public AccountScope(ICurrentUser currentUser) => _currentUser = currentUser;

    public async Task<AccountsInView> ResolveAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        Guid userId = _currentUser.UserId;

        SharingSettings? settings = await context.SharingSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        bool justMine = settings is { IncludeShared: false };

        return await ResolveAsync(context, userId, onlyChangeable: false, justMine, cancellationToken);
    }

    public Task<AccountsInView> ResolveAllAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ResolveAsync(context, _currentUser.UserId, onlyChangeable: false, justMine: false, cancellationToken);
    }

    public Task<AccountsInView> ResolveChangeableAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ResolveAsync(context, _currentUser.UserId, onlyChangeable: true, justMine: false, cancellationToken);
    }

    /// <summary>
    /// The accounts <paramref name="userId"/> can change, as <see cref="ResolveChangeableAsync"/> gives the current
    /// user, for work done on someone else's behalf, such as re-applying their rule after another person edited it.
    /// </summary>
    internal static Task<AccountsInView> ResolveChangeableForAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken) =>
        ResolveAsync(context, userId, onlyChangeable: true, justMine: false, cancellationToken);

    /// <summary>
    /// Every account <paramref name="userId"/> sees, as <see cref="ResolveAllAsync"/> gives the current user, for
    /// working out their figures the same way for someone they share with, such as what a shared debt owes.
    /// </summary>
    internal static Task<AccountsInView> ResolveAllForAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken) =>
        ResolveAsync(context, userId, onlyChangeable: false, justMine: false, cancellationToken);

    /// <param name="onlyChangeable">Only the shared accounts they're allowed to change.</param>
    /// <param name="justMine">Only their own, and joint accounts they're one of the two of.</param>
    private static async Task<AccountsInView> ResolveAsync(FinanceTrackerContext context, Guid userId,
        bool onlyChangeable, bool justMine, CancellationToken cancellationToken)
    {
        // Read once, every view needs to know which copies are hidden
        List<JointCopies> joints = await JointCopiesAsync(context, cancellationToken);
        List<Guid> hidden = joints.Select(static x => x.HiddenId).ToList();

        Dictionary<Guid, Guid> owners = [];
        HashSet<Guid> changeable = [];

        // Their copy of a joint account is hidden, the shown one is theirs to change as much as the other person's
        foreach (JointCopies joint in joints.Where(x => x.HiddenOwner == userId))
        {
            owners[joint.ShownId] = joint.ShownOwner;
            changeable.Add(joint.ShownId);
        }

        if (!justMine)
        {
            foreach ((Guid AccountId, Guid OwnerId, ShareMode Mode) share in
                     await SharesWithAsync(context, userId, onlyChangeable, cancellationToken))
            {
                if (hidden.Contains(share.AccountId))
                {
                    continue;
                }

                owners[share.AccountId] = share.OwnerId;
                if (share.Mode == ShareMode.AllowChanges)
                {
                    changeable.Add(share.AccountId);
                }
            }
        }

        List<Guid> ownIds = await context.BankAccounts.OwnedBy(userId).Select(static x => x.Id)
            .ToListAsync(cancellationToken);
        HashSet<Guid> ownHidden = ownIds.Intersect(hidden).ToHashSet();
        HashSet<Guid> ownShown = ownIds.Except(ownHidden).ToHashSet();

        List<JointCopies> filling = joints
            .Where(x => ownShown.Contains(x.ShownId) || owners.ContainsKey(x.ShownId))
            .ToList();

        // Another person's hidden copy filling in for an account here may move money to their private accounts
        Dictionary<Guid, Guid> othersAccounts = new(owners);
        foreach (JointCopies joint in filling.Where(x => x.HiddenOwner != userId))
        {
            othersAccounts[joint.HiddenId] = joint.HiddenOwner;
        }

        return new AccountsInView(userId, ownShown, owners, changeable,
            await OwnersWithPrivateAccountsAsync(context, othersAccounts, hidden, cancellationToken), ownHidden,
            filling.ToDictionary(static x => x.HiddenId, static x => x.ShownId), justMine);
    }

    /// <summary>
    /// Every joint account's two copies and who owns each.
    /// </summary>
    private static async Task<List<JointCopies>> JointCopiesAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        var joints = await (
                from joint in context.JointAccounts
                join hiddenCopy in context.BankAccounts on joint.HiddenAccountId equals hiddenCopy.Id
                join shownCopy in context.BankAccounts on joint.ShownAccountId equals shownCopy.Id
                select new
                {
                    joint.HiddenAccountId, HiddenOwner = hiddenCopy.UserId, joint.ShownAccountId,
                    ShownOwner = shownCopy.UserId
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return joints
            .Select(static x => new JointCopies(x.HiddenAccountId, x.HiddenOwner, x.ShownAccountId, x.ShownOwner))
            .ToList();
    }

    /// <summary>
    /// The accounts the other person in an accepted link shares with them that are still that person's own, so a
    /// share left behind by an account changing hands never shows it.
    /// </summary>
    private static async Task<List<(Guid AccountId, Guid OwnerId, ShareMode Mode)>> SharesWithAsync(
        FinanceTrackerContext context, Guid userId, bool onlyChangeable, CancellationToken cancellationToken)
    {
        var shared = await (
                from share in context.Shares
                join link in context.UserLinks on share.LinkId equals link.Id
                join account in context.BankAccounts on share.ItemId equals account.Id
                where share.ItemType == ShareItemType.BankAccount
                      && link.Status == UserLinkStatus.Accepted
                      && (link.UserId == userId || link.InviteeId == userId)
                      && share.UserId != userId
                      && account.UserId == share.UserId
                      && (!onlyChangeable || share.Mode == ShareMode.AllowChanges)
                select new { AccountId = account.Id, OwnerId = account.UserId, share.Mode })
            .Distinct()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return shared.Select(static x => (x.AccountId, x.OwnerId, x.Mode)).ToList();
    }

    /// <summary>
    /// Which of the owners of <paramref name="shared"/> have other accounts, not among them, the hidden copies of
    /// joint accounts aside.
    /// </summary>
    private static async Task<IReadOnlySet<Guid>> OwnersWithPrivateAccountsAsync(FinanceTrackerContext context,
        Dictionary<Guid, Guid> shared, List<Guid> hidden, CancellationToken cancellationToken)
    {
        if (shared.Count == 0)
        {
            return new HashSet<Guid>();
        }

        List<Guid> ownerIds = shared.Values.Distinct().ToList();
        List<Guid> visibleIds = shared.Keys.ToList();
        List<Guid> withPrivate = await context.BankAccounts
            .Where(x => ownerIds.Contains(x.UserId) && !visibleIds.Contains(x.Id) && !hidden.Contains(x.Id))
            .Select(static x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return withPrivate.ToHashSet();
    }
}
