using FinanceTracker.Infrastructure.Automation;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// The jobs that run on a schedule, with the schedule each is given the first time the app starts, declared to Quartz
/// in <c>AddBackgroundJobs</c>. Changes made on the automation page are kept in Quartz's store from then on.
/// </summary>
public static class ScheduledJobs
{
    public static IReadOnlyList<ScheduledJob> All { get; } =
    [
        new(JobNames.SyncAll, "Performs background syncing of your banking data", "0 0 */4 * * ?", 1, [10, 20, 30]),

        new(JobNames.MonitorTokens, "Performs background monitoring of your provider access tokens for expiration",
            "0 0 */12 * * ?", 1, [10, 20, 30]),

        new(JobNames.MoveGoalsOn,
            "Moves the savings goals of your yearly events on to their next date once each passes", "0 0 2 * * ?",
            1, [10, 20, 30]),

        new(JobNames.PruneNotifications, "Deletes notifications you read more than 90 days ago", "0 30 2 * * ?",
            1, [10, 20, 30])
    ];
}
