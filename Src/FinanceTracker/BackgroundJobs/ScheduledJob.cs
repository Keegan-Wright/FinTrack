namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// A job that runs on a schedule, and the schedule it starts with, see <see cref="ScheduledJobs"/>.
/// </summary>
/// <param name="Expression">A Quartz cron expression in UTC.</param>
public sealed record ScheduledJob(string Function, string Description, string Expression, int Retries,
    IReadOnlyList<int> RetryIntervals);
