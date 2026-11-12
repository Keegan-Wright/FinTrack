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
            return Mine(userId);
        }

        // Shared with them by the other person in an accepted link, and still that person's own account, so a share
        // left behind by an account changing hands never shows it
        var shared = await (
                from share in context.Shares
                join link in context.UserLinks on share.LinkId equals link.Id
                join account in context.BankAccounts on share.ItemId equals account.Id
                where share.ItemType == ShareItemType.BankAccount
                      && link.Status == UserLinkStatus.Accepted
                      && (link.UserId == userId || link.InviteeId == userId)
                      && share.UserId != userId
                      && account.UserId == share.UserId
                select new { AccountId = account.Id, OwnerId = account.UserId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return new AccountsInView(userId, shared.Select(static x => x.AccountId).ToList(),
            shared.ToDictionary(static x => x.AccountId, static x => x.OwnerId));
    }

    private static AccountsInView Mine(Guid userId) => new(userId, [], new Dictionary<Guid, Guid>());
}
