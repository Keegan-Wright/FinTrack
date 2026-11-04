using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Automation;
using Quartz;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Runs <see cref="JobNames.SyncUser"/> now for the user, so it shows on the automation page as a manual run.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IOpenBankingSyncScheduler>]
public sealed class QuartzOpenBankingSyncScheduler : IOpenBankingSyncScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;

    public QuartzOpenBankingSyncScheduler(ISchedulerFactory schedulerFactory) => _schedulerFactory = schedulerFactory;

    public async Task ScheduleUserSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        JobDataMap data = new() { { JobNames.UserIdKey, userId.ToString() } };
        await scheduler.TriggerJob(new JobKey(JobNames.SyncUser), data, cancellationToken);
    }
}
