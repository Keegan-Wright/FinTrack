using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;
using DomainShareMode = FinanceTracker.Domain.Sharing.ShareMode;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// What follows a change in a link, see <see cref="SharingService"/>. Telling the other person, matching transfers and
/// running rules again as reach changes, and taking off what each put on the other's transactions.
/// </summary>
internal sealed class SharingEffects : ServiceBase<SharingService>
{
    /// <summary>
    /// Where in the app links are managed, for the notifications about them.
    /// </summary>
    private const string SharingPath = "Sharing";

    private readonly IServiceScopeFactory _scopeFactory;

    public SharingEffects(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SharingService> logger,
        IServiceScopeFactory scopeFactory)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Raises the notification for <paramref name="userId"/>, acting as them so it follows their own settings.
    /// </summary>
    public async Task NotifyAsync(Guid userId, NotificationKind kind, string key, string title, string message,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
        await scope.ServiceProvider.GetRequiredService<INotifier>()
            .NotifyAsync(kind, key, title, message, SharingPath, cancellationToken);
    }

    /// <summary>
    /// Tells the other person in <paramref name="link"/> what the user did.
    /// </summary>
    /// <param name="title">The title, given the user's name.</param>
    public async Task NotifyOtherAsync(FinanceTrackerContext context, DomainLink link, string key,
        Func<string, string> title, string message, CancellationToken cancellationToken,
        NotificationKind kind = NotificationKind.JointAccount)
    {
        FinanceTrackerUser user = await context.Users.AsNoTracking().SingleAsync(x => x.Id == UserId,
            cancellationToken);
        await NotifyAsync(link.OtherThan(UserId), kind, key, title(PersonNames.Full(user)), message,
            cancellationToken);
    }

    /// <summary>
    /// Runs the rules again over <paramref name="accounts"/>, each as its owner, once who can change them changed.
    /// Rules that no longer reach them take back what they gave, and those that now do run.
    /// </summary>
    public async Task RerunRulesAsync(List<(Guid OwnerId, Guid AccountId)> accounts,
        CancellationToken cancellationToken)
    {
        foreach (IGrouping<Guid, Guid> owner in accounts.GroupBy(static x => x.OwnerId, static x => x.AccountId))
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(owner.Key);
            await scope.ServiceProvider.GetRequiredService<IRuleApplicationService>()
                .ReapplyToAccountsAsync(owner.Distinct().ToList(), cancellationToken);
        }
    }

    /// <summary>
    /// Matches both people's transfers again once what's shared between them changed. Money moved between their
    /// accounts is a transfer only while one of the two accounts is shared, so pairs come and go with the shares.
    /// </summary>
    public async Task RematchTransfersAsync(FinanceTrackerContext context, DomainLink link,
        CancellationToken cancellationToken)
    {
        await CrossLinkTransfers.ClearUnpairableAsync(context, link.UserId, link.InviteeId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Each as themselves, they're in different groups once the link has ended
        foreach (Guid userId in new[] { link.UserId, link.InviteeId })
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            await scope.ServiceProvider.GetRequiredService<ITransferMatchingService>().MatchAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Takes each person's tags, bills and debts off the other's transactions, those they put on while allowed to
    /// change them. Neither could see the other's afterwards, and a payment to a bill they can't see would keep their
    /// own bills from matching it.
    /// </summary>
    /// <param name="shares">The link's shares, those for what was put on each transaction among them, see
    /// <see cref="AnnotationShares"/>.</param>
    public static async Task TakeOffAnnotationsAsync(FinanceTrackerContext context, DomainLink link,
        List<Share> shares, CancellationToken cancellationToken)
    {
        foreach (Share share in shares.Where(static x => x.ItemType != ShareItemType.BankAccount))
        {
            await AnnotationShares.TakeOffTransactionsOfAsync(context, link.OtherThan(share.UserId), share.ItemType,
                share.ItemId, cancellationToken);
        }
    }

    /// <summary>
    /// The accounts in <paramref name="link"/> one person lets the other change, so the other's rules reach them. Those
    /// shared with changes allowed, and both copies of each merged joint account. Each with its owner.
    /// </summary>
    public static async Task<List<(Guid OwnerId, Guid AccountId)>> ChangeableAcrossAsync(
        FinanceTrackerContext context, DomainLink link, CancellationToken cancellationToken)
    {
        var shared = await context.Shares.AsNoTracking()
            .Where(x => x.LinkId == link.Id && x.ItemType == ShareItemType.BankAccount &&
                        x.Mode == DomainShareMode.AllowChanges)
            .Select(static x => new { x.UserId, x.ItemId })
            .ToListAsync(cancellationToken);

        List<JointAccount> joints = await context.JointAccounts.AsNoTracking()
            .Where(x => x.LinkId == link.Id)
            .ToListAsync(cancellationToken);

        return
        [
            .. shared.Select(static x => (x.UserId, x.ItemId)),
            .. await CopiesOfAsync(context, joints, cancellationToken)
        ];
    }

    /// <summary>
    /// Both copies of each of <paramref name="joints"/>, each with its owner.
    /// </summary>
    public static async Task<List<(Guid OwnerId, Guid AccountId)>> CopiesOfAsync(FinanceTrackerContext context,
        List<JointAccount> joints, CancellationToken cancellationToken)
    {
        List<Guid> accountIds = joints.SelectMany(static x => new[] { x.ShownAccountId, x.HiddenAccountId }).ToList();
        var owners = await context.BankAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .Select(static x => new { x.Id, x.UserId })
            .ToListAsync(cancellationToken);

        return owners.Select(static x => (x.UserId, x.Id)).ToList();
    }
}
