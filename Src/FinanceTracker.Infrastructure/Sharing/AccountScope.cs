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
        if (settings is { IncludeShared: false })
        {
            return AccountsInView.Mine(userId, await OwnAccountIdsAsync(context, userId, cancellationToken));
        }

        return await SharedAsync(context, userId, onlyChangeable: false, cancellationToken);
    }

    public Task<AccountsInView> ResolveAllAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SharedAsync(context, _currentUser.UserId, onlyChangeable: false, cancellationToken);
    }

    public Task<AccountsInView> ResolveChangeableAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SharedAsync(context, _currentUser.UserId, onlyChangeable: true, cancellationToken);
    }

    /// <summary>
    /// Their own accounts, and those the other person in an accepted link shares with them that are still that
    /// person's own, so a share left behind by an account changing hands never shows it.
    /// </summary>
    /// <param name="onlyChangeable">Only the shared accounts they're allowed to change.</param>
    private static async Task<AccountsInView> SharedAsync(FinanceTrackerContext context, Guid userId,
        bool onlyChangeable, CancellationToken cancellationToken)
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
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Guid> owners = [];
        HashSet<Guid> changeable = [];
        foreach (var account in shared)
        {
            owners[account.AccountId] = account.OwnerId;
            if (account.Mode == ShareMode.AllowChanges)
            {
                changeable.Add(account.AccountId);
            }
        }

        return new AccountsInView(userId, await OwnAccountIdsAsync(context, userId, cancellationToken), owners,
            changeable, await OwnersWithPrivateAccountsAsync(context, owners, cancellationToken));
    }

    /// <summary>
    /// Which of the owners of <paramref name="shared"/> have other accounts, not among them.
    /// </summary>
    private static async Task<IReadOnlySet<Guid>> OwnersWithPrivateAccountsAsync(FinanceTrackerContext context,
        Dictionary<Guid, Guid> shared, CancellationToken cancellationToken)
    {
        if (shared.Count == 0)
        {
            return new HashSet<Guid>();
        }

        List<Guid> ownerIds = shared.Values.Distinct().ToList();
        List<Guid> sharedIds = shared.Keys.ToList();
        List<Guid> withPrivate = await context.BankAccounts
            .Where(x => ownerIds.Contains(x.UserId) && !sharedIds.Contains(x.Id))
            .Select(static x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return withPrivate.ToHashSet();
    }

    private static async Task<IReadOnlySet<Guid>> OwnAccountIdsAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken) =>
        (await context.BankAccounts.OwnedBy(userId).Select(static x => x.Id).ToListAsync(cancellationToken))
        .ToHashSet();
}
