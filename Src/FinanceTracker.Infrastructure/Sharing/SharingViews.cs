using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Sharing;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISharingViews>]
public sealed class SharingViews : ServiceBase<SharingService>, ISharingViews
{
    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    public SharingViews(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SharingService> logger)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<SharingView> GetViewAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        return await ViewAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SharingView> ChangeViewAsync(ChangeSharingView request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        SharingSettings? settings =
            await context.SharingSettings.OwnedBy(UserId).SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new SharingSettings(UserId);
            context.SharingSettings.Add(settings);
        }

        settings.ShowShared(request.IncludeShared);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChangedTheir(UserId, "changed", "sharing view");

        return await ViewAsync(context, cancellationToken);
    }

    /// <summary>
    /// Their choice, and whether anything's shared with them whichever they chose.
    /// </summary>
    private async Task<SharingView> ViewAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        SharingSettings? settings = await context.SharingSettings.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        bool includesShared = settings?.IncludeShared ?? true;

        bool hasShared = await context.Shares.AnyAsync(x =>
            x.ItemType == ShareItemType.BankAccount && x.UserId != UserId &&
            context.UserLinks.Any(l => l.Id == x.LinkId && l.Status == UserLinkStatus.Accepted &&
                                       (l.UserId == UserId || l.InviteeId == UserId)), cancellationToken);

        return new SharingView { IncludesShared = includesShared, HasSharedAccounts = hasShared };
    }
}
