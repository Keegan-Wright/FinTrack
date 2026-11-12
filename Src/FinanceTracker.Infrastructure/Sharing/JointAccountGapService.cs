using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Sharing;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IJointAccountGapService>]
public sealed class JointAccountGapService : ServiceBase<JointAccountGapService>, IJointAccountGapService
{
    private readonly TimeProvider _timeProvider;
    private readonly ITransferMatchingService _transferMatching;

    public JointAccountGapService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<JointAccountGapService> logger,
        TimeProvider timeProvider, ITransferMatchingService transferMatching)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _transferMatching = transferMatching;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        List<Guid> changed = [];
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            Guid userId = UserId;
            List<JointAccount> joints = await (
                    from joint in context.JointAccounts
                    join shown in context.BankAccounts on joint.ShownAccountId equals shown.Id
                    join hidden in context.BankAccounts on joint.HiddenAccountId equals hidden.Id
                    where shown.UserId == userId || hidden.UserId == userId
                    select joint)
                .ToListAsync(cancellationToken);

            DateTime now = _timeProvider.UtcNow;
            foreach (JointAccount joint in joints)
            {
                changed.AddRange(await JointAccountGaps.RefreshAsync(context, joint, now, cancellationToken));
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // The pairs either of them sees around what filled in or stopped filling in
        if (changed.Count > 0)
        {
            await _transferMatching.MatchAroundAsync(changed.Distinct().ToList(), cancellationToken);
        }
    }
}
