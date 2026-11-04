using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.PayPeriods;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IPaySyncService>]
public class PaySyncService : ServiceBase<PaySyncService>, IPaySyncService
{
    private readonly TimeProvider _timeProvider;

    public PaySyncService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<PaySyncService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IReadOnlyList<DetectedPay> detected =
            await PayTransactions.DetectAsync(context, UserId, _timeProvider, cancellationToken);

        PaySchedule? schedule = await context.PaySchedules.OwnedBy(UserId).SingleOrDefaultAsync(cancellationToken);

        if (schedule is null)
        {
            if (detected.Count == 0)
            {
                return false;
            }

            schedule = new PaySchedule(UserId, detected[0]);
            context.PaySchedules.Add(schedule);
        }
        else
        {
            schedule.Update(detected);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogSyncedPay(UserId, detected.Count, schedule.PayDays.Count, schedule.IsChosen, schedule.IsOff);
        return true;
    }
}
