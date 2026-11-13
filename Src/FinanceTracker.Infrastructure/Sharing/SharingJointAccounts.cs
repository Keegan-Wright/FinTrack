using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Merging and splitting the joint accounts two linked people have both connected, see <see cref="SharingService"/>
/// and <see cref="JointAccounts"/>, with what follows: transfers matched again, rules run again over both copies, and
/// the other person told.
/// </summary>
internal sealed class SharingJointAccounts : ServiceBase<SharingService>
{
    private readonly TimeProvider _timeProvider;
    private readonly SharingEffects _effects;
    private readonly SharingChoices _choices;

    public SharingJointAccounts(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SharingService> logger,
        TimeProvider timeProvider, SharingEffects effects, SharingChoices choices)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _effects = effects;
        _choices = choices;
    }

    public async Task<LinkShares> MergeAsync(Guid linkId, MergeJointAccounts request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink link = await SharingLinks.AcceptedLinkAsync(context, UserId, linkId, cancellationToken)
                          ?? throw new NotFoundException($"No accepted link {linkId} was found.");

        List<(Guid Yours, Guid Theirs)> pairs =
            request.Accounts.Select(static x => (x.YourAccountId, x.TheirAccountId)).ToList();
        List<JointAccount> merged = await JointAccounts.MergeAsync(context, link, UserId, pairs,
            _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        foreach (JointAccount joint in merged)
        {
            Logger.LogChanged(UserId, "merged", "joint account", joint.Id);
        }

        // Once for all of them, it matches everything both of them have
        await _effects.RematchTransfersAsync(context, link, cancellationToken);
        await _effects.RerunRulesAsync(await SharingEffects.CopiesOfAsync(context, merged, cancellationToken),
            cancellationToken);

        string name = await MergedNamesAsync(context, merged, cancellationToken);
        await _effects.NotifyOtherAsync(context, link, $"joint-account:{merged[0].Id}:merged",
            person => $"{person} merged {name}",
            "You'd both connected them, so each is counted once now. You each see and tag the same copy, and yours " +
            "keeps syncing in the background. Either of you can split one again on the Sharing page.",
            cancellationToken);

        return await _choices.LinkSharesAsync(context, link, cancellationToken);
    }

    public async Task<LinkShares> SplitAsync(Guid linkId, Guid jointAccountId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink link = await SharingLinks.AcceptedLinkAsync(context, UserId, linkId, cancellationToken)
                          ?? throw new NotFoundException($"No accepted link {linkId} was found.");
        JointAccount joint = await context.JointAccounts
                                 .SingleOrDefaultAsync(x => x.Id == jointAccountId && x.LinkId == link.Id,
                                     cancellationToken)
                             ?? throw new NotFoundException($"No joint account {jointAccountId} was found.");

        string name = await AccountNameAsync(context, joint.ShownAccountId, cancellationToken);
        List<(Guid OwnerId, Guid AccountId)> copies =
            await SharingEffects.CopiesOfAsync(context, [joint], cancellationToken);
        await JointAccounts.SplitAsync(context, link, joint, _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "split", "joint account", joint.Id);

        await _effects.RematchTransfersAsync(context, link, cancellationToken);
        await _effects.RerunRulesAsync(copies, cancellationToken);

        await _effects.NotifyOtherAsync(context, link, $"joint-account:{joint.Id}:split",
            person => $"{person} split your joint account {name}",
            "You each see your own copy again, with its tags as they were. It counts in both of your totals until " +
            "you merge it again.",
            cancellationToken);

        return await _choices.LinkSharesAsync(context, link, cancellationToken);
    }

    /// <summary>
    /// Splits every joint account merged in <paramref name="link"/>, and saves.
    /// </summary>
    /// <returns>Whether there were any to split.</returns>
    public async Task<bool> SplitAllAsync(FinanceTrackerContext context, DomainLink link,
        CancellationToken cancellationToken)
    {
        List<JointAccount> joints = await context.JointAccounts.Where(x => x.LinkId == link.Id)
            .ToListAsync(cancellationToken);
        if (joints.Count == 0)
        {
            return false;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (JointAccount joint in joints)
        {
            await JointAccounts.SplitAsync(context, link, joint, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// What was merged, for the notification, e.g. <c>your joint account Joint</c> or <c>3 joint accounts</c>.
    /// </summary>
    private static async Task<string> MergedNamesAsync(FinanceTrackerContext context, List<JointAccount> merged,
        CancellationToken cancellationToken)
    {
        if (merged.Count > 1)
        {
            return $"{merged.Count} of your joint accounts";
        }

        string name = await AccountNameAsync(context, merged[0].ShownAccountId, cancellationToken);
        return $"your joint account {name}";
    }

    private static async Task<string> AccountNameAsync(FinanceTrackerContext context, Guid accountId,
        CancellationToken cancellationToken) =>
        await context.BankAccounts.AsNoTracking()
            .Where(x => x.Id == accountId)
            .Select(static x => x.DisplayName)
            .SingleAsync(cancellationToken);
}
